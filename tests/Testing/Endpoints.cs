namespace Testing;

public static class Endpoints
{
    public static class Health
    {
        public static string Ready() => "health";
    }

    public static class OpenApi
    {
        public const string V1 = "openapi/v1.json";
    }

    public static class Organisations
    {
        private static string Root => "organisations";

        public static string Get(string id) => $"{Root}/{id}";

        public static string Search() => Root;

        public static string Put(string id) => Get(id);

        private static string RegistrationsGet(string id) => $"{Get(id)}/registrations";

        public static string RegistrationsPost(string id, string type, string submissionYear) =>
            $"{RegistrationsGet(id)}/{type}-{submissionYear}";

        public static string RegistrationsPut(string id, string type, string submissionYear) =>
            RegistrationsPost(id, type, submissionYear);

        public static string RegistrationsDelete(string id, string type, string submissionYear) =>
            RegistrationsPost(id, type, submissionYear);
    }
}
