namespace Testing;

public static class Endpoints
{
    public static class OpenApi
    {
        public const string V1 = "openapi/v1.json";
    }

    public static class Organisations
    {
        public static string Get(string id) => $"organisations/{id}";
    }
}
