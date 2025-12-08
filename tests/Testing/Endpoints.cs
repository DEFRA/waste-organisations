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

        public static string Get(Guid id) => Get(id.ToString());

        public static string Search(EndpointQuery? query = null) => $"{Root}{query}";

        public static string Put(string id) => Get(id);

        public static string Put(Guid id) => Put(id.ToString());

        private static string RegistrationsGet(string id) => $"{Get(id)}/registrations";

        public static string RegistrationsPut(string id, string type, string registrationYear) =>
            $"{RegistrationsGet(id)}/{type}-{registrationYear}";

        public static string RegistrationsDelete(string id, string type, string registrationYear) =>
            RegistrationsPut(id, type, registrationYear);
    }
}
