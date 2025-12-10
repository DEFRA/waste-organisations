using Api.Dtos;
using Organisation = Api.Data.Entities.Organisation;

namespace Api.Services;

public interface IOrganisationService
{
    Task<Organisation?> Get(Guid id, CancellationToken cancellationToken);

    Task<Organisation> Create(Organisation organisation, CancellationToken cancellationToken);

    Task<Organisation> Update(Organisation organisation, CancellationToken cancellationToken);

    Task<List<Organisation>> Search(
        List<RegistrationType> registrationTypes,
        List<int> registrationYears,
        List<RegistrationStatus> registrationStatuses,
        CancellationToken cancellationToken
    );
}
