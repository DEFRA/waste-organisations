#!/usr/bin/env bash
# The deterministic half of the slice-deliver skill: it reports which stage a
# slice is at, from the real state of git, GitHub and ralph, and it starts ralph
# detached so a run outlives the chat tool's command timeout. Everything that
# needs judgement (pull request text, review, reporting) stays in SKILL.md.
#
# Usage:
#   slice.sh status <spec> <slice>   facts, blockers, warnings and the stage
#   slice.sh probe                   check the agent ralph uses can run headless
#   slice.sh run <spec> <slice>      start or continue ralph for the slice
#   slice.sh wait [seconds]          wait for ralph to exit (default 540)
#   slice.sh record <key> <value>    record a stage result (see RECORD_KEYS)
#   slice.sh reset                   forget the slice in flight (plan untouched)
#
# Written for bash 3.2, which is what macOS ships.
set -euo pipefail

# Resolved before any cd, because the detached run calls this script again.
SCRIPT_PATH="$(cd "$(dirname "$0")" && pwd)/$(basename "$0")"

STATE_DIR=".ralph/slice-deliver"
STATE_FILE="$STATE_DIR/state"
LOG_FILE="$STATE_DIR/ralph.log"
PID_FILE="$STATE_DIR/ralph.pid"
EXIT_FILE="$STATE_DIR/ralph.exit"
AUTO_STATE_FILE=".ralph/auto-state"
PLAN_FILE="IMPLEMENTATION_PLAN.md"
RECORD_KEYS="gate_sha preflight_sha preflight_verdict done note"
COMPOSE_POSTGRES_PORT=5432

die() {
    echo "error: $*" >&2
    exit 1
}

have() {
    local tool=$1
    command -v "$tool" > /dev/null 2>&1
}

enter_repo_root() {
    local root
    root=$(git rev-parse --show-toplevel 2> /dev/null) || die "not inside a git repository"
    cd "$root"
}

state_get() {
    local key=$1
    [[ -f $STATE_FILE ]] || return 0
    sed -n "s/^$key=//p" "$STATE_FILE" | tail -n 1
}

state_set() {
    local key=$1 value=$2 kept
    mkdir -p "$STATE_DIR"
    kept=$(grep -v "^$key=" "$STATE_FILE" 2> /dev/null || true)
    {
        [[ -z $kept ]] || printf '%s\n' "$kept"
        printf '%s=%s\n' "$key" "$value"
    } > "$STATE_FILE"
}

# Accepts a path, or a spec number with or without leading zeros.
resolve_spec() {
    local arg=$1 padded matches count
    if [[ -f $arg ]]; then
        echo "$arg"
        return
    fi
    [[ $arg =~ ^[0-9]+$ ]] || die "spec '$arg' is neither a file nor a number"
    padded=$(printf '%04d' "$((10#$arg))")
    matches=$(ls specs/"$padded"-*.md 2> /dev/null || true)
    count=$(printf '%s' "$matches" | grep -c . || true)
    [[ $count -eq 1 ]] || die "expected one spec matching specs/$padded-*.md, found $count"
    echo "$matches"
}

story_key() {
    local spec=$1
    basename "$spec" | grep -oE '[A-Z]+-[0-9]+' | head -n 1 || true
}

# Prints slice_total, slice_work and slice_verified_by for one row of the
# spec's "Work breakdown" table. slice_work is empty when the row is missing.
read_slice() {
    local spec=$1 wanted=$2
    awk -v want="$wanted" '
        function trim(s) { gsub(/^[ \t]+|[ \t]+$/, "", s); return s }
        /^### Work breakdown[ \t]*$/ { in_section = 1; next }
        in_section && /^(#|##|###) / { in_section = 0 }
        in_section && /^\|/ {
            n = split($0, cell, "|")
            id = trim(cell[2])
            if (id ~ /^[0-9]+$/) {
                total++
                if (id == want) {
                    work = trim(cell[3])
                    verified = cell[4]
                    for (i = 5; i < n; i++) verified = verified "|" cell[i]
                    verified = trim(verified)
                }
            }
        }
        END {
            print "slice_total=" total + 0
            print "slice_work=" work
            print "slice_verified_by=" verified
        }
    ' "$spec"
}

# Counts plan items the way ralph does: below "## Items" when that heading
# exists, so the entry template above it never counts as work.
plan_count() {
    local marker=$1
    [[ -f $PLAN_FILE ]] || {
        echo 0
        return
    }
    if grep -qE '^## Items[[:space:]]*$' "$PLAN_FILE"; then
        sed -n '/^## Items[[:space:]]*$/,$p' "$PLAN_FILE"
    else
        cat "$PLAN_FILE"
    fi | grep -c "$marker" || true
}

ralph_alive() {
    local pid process
    [[ -f $PID_FILE ]] || return 1
    pid=$(cat "$PID_FILE")
    [[ $pid =~ ^[0-9]+$ ]] || return 1
    kill -0 "$pid" 2> /dev/null || return 1
    # Guards against a recycled pid after a reboot.
    process=$(ps -p "$pid" -o command= 2> /dev/null || true)
    [[ $process == *ralph* ]]
}

# owner/name from the origin URL. gh cannot work it out by itself when the
# remote uses an SSH host alias, so every gh call names the repository.
origin_repo() {
    local url
    url=$(git remote get-url origin 2> /dev/null) || return 0
    url=${url%.git}
    printf '%s\n' "$url" | sed -E 's#^.*[:/]([^/:]+/[^/]+)$#\1#'
}

ralph_backend() {
    local config=""
    if [[ -f ralph.yaml ]]; then
        config=ralph.yaml
    elif [[ -f "${RALPH_CONFIG_DIR:-$HOME/.config/ralph}/config.yaml" ]]; then
        config="${RALPH_CONFIG_DIR:-$HOME/.config/ralph}/config.yaml"
    fi
    if [[ -n $config ]] && have yq; then
        yq -r '.plan.backend // .defaults.backend // "claude"' "$config"
    else
        echo claude
    fi
}

# A chat session exports its own effort level, session id and entry point.
# ralph's agents must run as they do from a terminal, so those are dropped;
# the variables a developer sets to authenticate or to pick a provider stay.
run_with_terminal_environment() {
    local name unset_args=()
    while IFS= read -r name; do
        [[ $name == CLAUDE* ]] || continue
        case $name in
            CLAUDE_CODE_OAUTH_TOKEN | CLAUDE_CODE_USE_* | CLAUDE_CONFIG_DIR) ;;
            *) unset_args+=(-u "$name") ;;
        esac
    done < <(env | cut -d= -f1)
    env ${unset_args[@]+"${unset_args[@]}"} "$@"
}

# Sets the facts every subcommand needs. Dies when the spec or slice is wrong,
# because nothing else can be trusted after that.
load_facts() {
    local spec_arg=$1 slice_arg=$2 slice_facts
    spec_file=$(resolve_spec "$spec_arg")
    slice=$slice_arg
    [[ $slice =~ ^[0-9]+$ ]] || die "slice '$slice' is not a number"
    story=$(story_key "$spec_file")
    slice_facts=$(read_slice "$spec_file" "$slice")
    slice_total=$(printf '%s\n' "$slice_facts" | sed -n 's/^slice_total=//p')
    slice_work=$(printf '%s\n' "$slice_facts" | sed -n 's/^slice_work=//p')
    slice_verified_by=$(printf '%s\n' "$slice_facts" | sed -n 's/^slice_verified_by=//p')
    [[ $slice_total -gt 0 ]] || die "$spec_file has no Work breakdown table"
    [[ -n $slice_work ]] || die "$spec_file has no slice $slice (it has $slice_total)"

    branch=$(git symbolic-ref --quiet --short HEAD || true)
    head_sha=$(git rev-parse HEAD)
    goal="Make an implementation plan for Slice $slice in $spec_file. Plan this slice only: its Work and Verified by cells in the spec's Work breakdown bound the scope. Earlier slices are already built and later slices are out of scope."

    state_spec=$(state_get spec_file)
    state_slice=$(state_get slice)
    state_branch=$(state_get branch)
    state_done=$(state_get done)
    state_is_this_slice=no
    [[ $state_spec == "$spec_file" && $state_slice == "$slice" ]] && state_is_this_slice=yes

    plan_open=$(plan_count '^- \[ \]')
    plan_done=$(plan_count '^- \[x\]')
    ralph_exit=""
    [[ -f $EXIT_FILE ]] && ralph_exit=$(cat "$EXIT_FILE")
    return 0
}

blockers=()
warnings=()

block() {
    local message=$1
    blockers+=("$message")
}

warn() {
    local message=$1
    warnings+=("$message")
}

# The pull request for the branch, newest first; closed and merged ones are
# included so a reused branch is noticed.
load_pull_request() {
    local json pr_head
    pr_number="" pr_url="" pr_state="" pr_draft="" pr_mergeable="" ci=""
    gh_login="" repo=$(origin_repo)
    have gh || return 0
    gh_login=$(gh api user --jq .login 2> /dev/null || true)
    [[ -n $gh_login && -n $repo && -n $branch ]] || return 0
    gh_can_push=$(gh api "repos/$repo" --jq .permissions.push 2> /dev/null || true)
    json=$(gh pr list -R "$repo" --head "$branch" --state all --limit 1 \
        --json number,url,state,isDraft,mergeable,headRefOid 2> /dev/null || true)
    [[ -n $json && $json != "[]" ]] || return 0
    # A merged or closed pull request belongs to this branch only while the
    # branch still holds its commits. A branch made again from main under the
    # same name, as after a squash merge, starts without one.
    pr_head=$(printf '%s' "$json" | jq -r '.[0].headRefOid // empty')
    if [[ $(printf '%s' "$json" | jq -r '.[0].state') != OPEN && -n $pr_head ]] &&
        ! git merge-base --is-ancestor "$pr_head" HEAD 2> /dev/null; then
        return 0
    fi
    pr_number=$(printf '%s' "$json" | jq -r '.[0].number')
    pr_url=$(printf '%s' "$json" | jq -r '.[0].url')
    pr_state=$(printf '%s' "$json" | jq -r '.[0].state')
    pr_draft=$(printf '%s' "$json" | jq -r '.[0].isDraft')
    pr_mergeable=$(printf '%s' "$json" | jq -r '.[0].mergeable // empty')
    [[ $pr_state == OPEN ]] || return 0
    ci=$(gh pr checks "$pr_url" --json bucket --jq '
        if length == 0 then "none"
        elif any(.[]; .bucket == "fail" or .bucket == "cancel") then "fail"
        elif any(.[]; .bucket == "pending") then "pending"
        else "pass" end' 2> /dev/null || true)
    [[ -n $ci ]] || ci=none
}

load_git_position() {
    local counts
    upstream=$(git rev-parse --abbrev-ref --symbolic-full-name '@{upstream}' 2> /dev/null || true)
    unpushed=""
    [[ -z $upstream ]] || unpushed=$(git rev-list --count "$upstream..HEAD")
    ahead_of_main="" behind_main=""
    if git rev-parse --verify --quiet origin/main > /dev/null; then
        counts=$(git rev-list --left-right --count origin/main...HEAD)
        behind_main=$(printf '%s' "$counts" | awk '{print $1}')
        ahead_of_main=$(printf '%s' "$counts" | awk '{print $2}')
    fi
    tree_dirty=no
    [[ -z $(git status --porcelain) ]] || tree_dirty=yes
    specs_dirty=no
    [[ -z $(git status --porcelain -- specs) ]] || specs_dirty=yes
}

compose_postgres_running() {
    [[ -n $(docker compose ps --status running -q postgres 2> /dev/null || true) ]]
}

# The stage is derived from what is true now, never from what an earlier run
# intended, so running the skill again always continues at the right place.
compute_stage() {
    if ralph_alive; then
        stage=build-running
    elif [[ $state_is_this_slice == no ]]; then
        stage=checks
    elif [[ -f $AUTO_STATE_FILE ]] || [[ -n $ralph_exit && $ralph_exit != 0 ]]; then
        stage=build-failed
    elif [[ $plan_open -gt 0 || $plan_done -eq 0 ]]; then
        stage=build-incomplete
    elif [[ -z $upstream || $unpushed -gt 0 ]]; then
        stage=push
    elif [[ $(state_get gate_sha) != "$head_sha" ]]; then
        stage=gate
    elif [[ $pr_state != OPEN ]]; then
        stage=pr
    elif [[ $(state_get preflight_sha) != "$head_sha" ]]; then
        stage=preflight
    elif [[ $(state_get preflight_verdict) != ready ]]; then
        stage=preflight-blocked
    elif [[ $pr_draft == true ]]; then
        case $ci in
            pass) stage=ready ;;
            fail) stage=ci-failed ;;
            *) stage=ci ;;
        esac
    elif [[ $state_done == yes ]]; then
        stage=done
    else
        stage=report
    fi
}

collect_blockers() {
    local tool
    for tool in ralph gh jq yq docker nc; do
        have "$tool" || block "tools: '$tool' is not installed or not on PATH"
    done

    if [[ -z $branch ]]; then
        block "branch: HEAD is detached; check out the slice's branch"
    elif [[ $branch == main || $branch == master ]]; then
        block "branch: on $branch; the slice needs its own branch"
    elif [[ -n $story && $branch != "$story"-* ]]; then
        block "branch: '$branch' does not start with $story-, the story of $spec_file"
    fi
    if [[ $state_is_this_slice == yes && -n $state_branch && $state_branch != "$branch" ]]; then
        block "branch: this slice was started on '$state_branch', not '$branch'"
    fi
    if [[ $state_is_this_slice == no && -n $state_spec && $state_done != yes ]]; then
        block "state: $state_spec slice $state_slice is still in flight; finish it, or run 'slice.sh reset' to abandon it"
    fi

    if [[ -z $gh_login ]]; then
        block "github: gh is not signed in (gh auth login)"
    elif [[ ${gh_can_push:-} != true ]]; then
        block "github: gh is signed in as '$gh_login', which cannot push to $repo (gh auth switch --user <account>)"
    fi
    if [[ $pr_state == MERGED || $pr_state == CLOSED ]]; then
        block "branch: pull request $pr_number for '$branch' is already $pr_state; start the slice on a new branch from main"
    fi
    if [[ $pr_state == OPEN && $pr_mergeable == CONFLICTING ]]; then
        block "github: pull request $pr_number conflicts with main; the branch must be brought up to date"
    fi

    case $stage in
        build-running | build-failed | build-incomplete) ;;
        *) [[ $tree_dirty == no ]] || block "tree: uncommitted changes; commit or stash them first" ;;
    esac
    if [[ $stage == checks || $stage == build-failed || $stage == build-incomplete ]]; then
        [[ $specs_dirty == no ]] || block "specs: specs/ has uncommitted changes; ralph refuses to plan over them"
    fi
    if [[ $stage == push || $stage == gate || $stage == pr ]]; then
        [[ ${ahead_of_main:-0} -gt 0 ]] || block "git: no commits ahead of origin/main, so there is nothing to open a pull request for"
    fi

    if have docker; then
        if ! docker info > /dev/null 2>&1; then
            block "docker: the Docker daemon is not running"
        elif have nc && nc -z 127.0.0.1 "$COMPOSE_POSTGRES_PORT" 2> /dev/null && ! compose_postgres_running; then
            block "postgres: something other than the Compose stack holds 127.0.0.1:$COMPOSE_POSTGRES_PORT; the integration tests would reach the wrong server (AGENTS.md, Postgres And Liquibase)"
        fi
    fi

    [[ ${behind_main:-0} -eq 0 ]] || warn "git: branch is $behind_main commit(s) behind origin/main"
    [[ ${ahead_of_main:-0} -ne 1 ]] || warn "git: one commit on the branch; a squash merge would take its subject, not the pull request title"
    if [[ $stage == checks && $plan_open -gt 0 ]]; then
        warn "ralph: the current plan has $plan_open open item(s); starting archives it under .ralph/"
    fi
    return 0
}

cmd_status() {
    [[ $# -eq 2 ]] || die "usage: slice.sh status <spec> <slice>"
    local spec_arg=$1 slice_arg=$2
    load_facts "$spec_arg" "$slice_arg"
    load_git_position
    load_pull_request
    compute_stage
    collect_blockers

    cat << EOF
spec_file=$spec_file
story=$story
slice=$slice
slice_total=$slice_total
slice_work=$slice_work
slice_verified_by=$slice_verified_by
branch=$branch
head=$head_sha
ahead_of_main=$ahead_of_main
behind_main=$behind_main
unpushed=$unpushed
tree_dirty=$tree_dirty
repo=$repo
gh_login=$gh_login
plan_open=$plan_open
plan_done=$plan_done
ralph_exit=$ralph_exit
ralph_failed_phase=$(sed -n 's/^phase_name=//p' "$AUTO_STATE_FILE" 2> /dev/null || true)
pr_number=$pr_number
pr_url=$pr_url
pr_state=$pr_state
pr_draft=$pr_draft
pr_mergeable=$pr_mergeable
ci=$ci
gate_sha=$(state_get gate_sha)
preflight_sha=$(state_get preflight_sha)
preflight_verdict=$(state_get preflight_verdict)
log=$LOG_FILE
EOF
    local line
    for line in ${warnings[@]+"${warnings[@]}"}; do echo "warning=$line"; done
    for line in ${blockers[@]+"${blockers[@]}"}; do echo "blocker=$line"; done
    echo "stage=$stage"
}

cmd_probe() {
    local backend out
    enter_repo_root
    backend=$(ralph_backend)
    if [[ $backend != claude ]]; then
        echo "probe=skipped (backend '$backend' has no probe; ralph reports a login failure itself)"
        return 0
    fi
    have claude || die "the claude CLI is not installed or not on PATH"
    out=$(run_with_terminal_environment claude -p "Reply with the single word OK" \
        --model sonnet --output-format json < /dev/null 2> /dev/null || true)
    if [[ $(printf '%s' "$out" | jq -r '.is_error' 2> /dev/null) == false ]]; then
        echo "probe=ok"
    else
        echo "probe=failed"
        echo "detail=$(printf '%s' "$out" | jq -r '.result // empty' 2> /dev/null | head -n 3)"
        return 1
    fi
}

cmd_run() {
    [[ $# -eq 2 ]] || die "usage: slice.sh run <spec> <slice>"
    local spec_arg=$1 slice_arg=$2
    load_facts "$spec_arg" "$slice_arg"
    load_git_position
    load_pull_request
    compute_stage
    collect_blockers

    case $stage in
        checks | build-failed | build-incomplete) ;;
        build-running) die "ralph is already running for this slice; use 'slice.sh wait'" ;;
        *) die "nothing to run: the slice is at stage '$stage'" ;;
    esac
    if [[ ${#blockers[@]} -gt 0 ]]; then
        printf 'blocker=%s\n' "${blockers[@]}" >&2
        die "not started: resolve the blockers above"
    fi

    # ralph auto refuses to run outside a container unless it is forced.
    local force=()
    [[ ${DEVCONTAINER:-false} == true ]] || force=(--force)

    # A plain 'ralph auto' archives the plan, so it is used only when nothing
    # of this slice has been built. A recorded failure resumes at its phase,
    # which ralph allows only once init has made the plan and progress files;
    # a run that died without recording one carries on building the open items.
    local command
    if [[ $stage == checks ]]; then
        rm -rf "$STATE_DIR"
        state_set spec_file "$spec_file"
        state_set slice "$slice"
        state_set branch "$branch"
        state_set started_at "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
        command=(ralph auto -g "$goal" ${force[@]+"${force[@]}"})
    elif [[ -f $AUTO_STATE_FILE && -f $PLAN_FILE && -f PROGRESS.md ]]; then
        command=(ralph auto --resume -g "$goal" ${force[@]+"${force[@]}"})
    elif [[ $((plan_open + plan_done)) -eq 0 ]]; then
        command=(ralph auto -g "$goal" ${force[@]+"${force[@]}"})
    else
        command=(ralph build -y)
    fi

    mkdir -p "$STATE_DIR"
    rm -f "$EXIT_FILE"
    printf '\n===== %s  %s =====\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "${command[*]}" >> "$LOG_FILE"
    # nohup, so the run survives the chat tool or its terminal closing.
    # shellcheck disable=SC2016  # the inner shell expands $@ and $0, not this one
    nohup bash -c '"$@"; echo $? > "$0"' "$EXIT_FILE" \
        bash "$SCRIPT_PATH" exec-clean "${command[@]}" >> "$LOG_FILE" 2>&1 < /dev/null &
    echo $! > "$PID_FILE"
    echo "started=${command[*]}"
    echo "pid=$(cat "$PID_FILE")"
    echo "log=$LOG_FILE"
}

cmd_wait() {
    local limit=${1:-540} waited=0
    [[ $limit =~ ^[0-9]+$ ]] || die "usage: slice.sh wait [seconds]"
    enter_repo_root
    while ralph_alive && [[ $waited -lt $limit ]]; do
        sleep 10
        waited=$((waited + 10))
    done
    if ralph_alive; then
        echo "ralph=running"
    elif [[ -f $EXIT_FILE ]]; then
        echo "ralph=exited"
        echo "exit=$(cat "$EXIT_FILE")"
    else
        echo "ralph=not-running"
    fi
    if [[ -f $LOG_FILE ]]; then
        echo "--- last lines of $LOG_FILE"
        tail -n 12 "$LOG_FILE" | cut -c1-240
    fi
}

cmd_record() {
    [[ $# -eq 2 ]] || die "usage: slice.sh record <key> <value>"
    local key=$1 value=$2
    enter_repo_root
    case " $RECORD_KEYS " in
        *" $key "*) ;;
        *) die "unknown key '$key' (one of: $RECORD_KEYS)" ;;
    esac
    [[ -f $STATE_FILE ]] || die "no slice in flight; nothing to record against"
    state_set "$key" "$value"
    echo "recorded $key=$value"
}

cmd_reset() {
    enter_repo_root
    ralph_alive && die "ralph is still running; stop it before resetting"
    rm -rf "$STATE_DIR"
    echo "reset: removed $STATE_DIR; the plan, the progress log and the branch are untouched"
}

case "${1:-}" in
    status)
        shift
        enter_repo_root
        cmd_status "$@"
        ;;
    run)
        shift
        enter_repo_root
        cmd_run "$@"
        ;;
    probe) cmd_probe ;;
    wait)
        shift
        cmd_wait "$@"
        ;;
    record)
        shift
        cmd_record "$@"
        ;;
    reset) cmd_reset ;;
    # Internal: runs ralph with the terminal environment (see cmd_run).
    exec-clean)
        shift
        run_with_terminal_environment "$@"
        ;;
    *)
        sed -n '2,15p' "$SCRIPT_PATH" | sed 's/^# \{0,1\}//'
        exit 1
        ;;
esac
