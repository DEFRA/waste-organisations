using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteOrganisations.Api.V1.Tests;

/// <summary>
/// Writes organisation documents straight to MongoDB, for the <c>LEG-*</c> scenarios only:
/// production holds data the API can no longer create, such as registrations without
/// timestamps. Documents are raw <see cref="BsonDocument"/>s in the stored shape, never the
/// API's entity types, so a v2 change to those types cannot change what is written here.
/// Dispose the instance to remove every document it inserted, and nothing else.
/// </summary>
public sealed class LegacyData : IAsyncDisposable
{
    /// <summary>Overrides the MongoDB connection string, for example to run against another host.</summary>
    public const string ConnectionStringVariable = "V1_TESTS_MONGO_URI";

    private const string DefaultConnectionString = "mongodb://127.0.0.1:27017";
    private const string DatabaseName = "waste-organisations";
    private const string CollectionName = "Organisation";

    private static readonly Lazy<IMongoCollection<BsonDocument>> Collection = new(Connect);

    private readonly List<Guid> _insertedIds = [];

    /// <summary>
    /// A stored organisation document with every field set, one registration per entry in
    /// <paramref name="registrations"/>, and <c>created</c> and <c>updated</c> both set to
    /// <paramref name="created"/>. Tests remove or change fields to build the legacy shape they need.
    /// </summary>
    public static BsonDocument Organisation(Guid id, DateTime created, params BsonDocument[] registrations) =>
        new()
        {
            { "_id", Id(id) },
            { "version", 1 },
            { "created", created },
            { "updated", created },
            { "name", "V1 Legacy Organisation Ltd" },
            { "tradingName", "V1 Legacy Trading Name" },
            { "businessCountry", "GB-ENG" },
            { "companiesHouseNumber", "87654321" },
            {
                "address",
                new BsonDocument
                {
                    { "addressLine1", "2 Legacy Street" },
                    { "addressLine2", "Legacy Estate" },
                    { "town", "Legacy Town" },
                    { "county", "Legacy County" },
                    { "postcode", "LE1 1GY" },
                    { "country", "United Kingdom" },
                }
            },
            { "registrations", new BsonArray(registrations) },
        };

    /// <summary>
    /// A stored registration without <c>created</c> or <c>updated</c>, as written before the API
    /// recorded registration timestamps.
    /// </summary>
    public static BsonDocument RegistrationWithoutTimestamps(string type, int registrationYear, string status) =>
        new()
        {
            { "type", type },
            { "registrationYear", registrationYear },
            { "status", status },
        };

    /// <summary>Inserts the document; its <c>_id</c> must be a GUID from <see cref="Id"/>.</summary>
    public async Task Insert(BsonDocument document)
    {
        var id = document["_id"].AsBsonBinaryData.ToGuid(GuidRepresentation.Standard);

        // Track the id first: a timeout can leave the write stored, and clean-up must still remove it.
        _insertedIds.Add(id);

        await Collection.Value.InsertOneAsync(document, cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Reads the stored document back, for checks on fields the API does not return, such as the
    /// organisation's own <c>updated</c>.
    /// </summary>
    public static async Task<BsonDocument?> Find(Guid id) =>
        await Collection
            .Value.Find(Builders<BsonDocument>.Filter.Eq("_id", Id(id)))
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

    /// <summary>The id in its stored form: a standard-representation (subtype 4) GUID.</summary>
    public static BsonBinaryData Id(Guid id) => new(id, GuidRepresentation.Standard);

    public async ValueTask DisposeAsync()
    {
        if (_insertedIds.Count == 0)
            return;

        // The test's cancellation token may already be cancelled by now; clean-up must still run.
        await Collection.Value.DeleteManyAsync(Builders<BsonDocument>.Filter.In("_id", _insertedIds.Select(Id)));
    }

    private static IMongoCollection<BsonDocument> Connect()
    {
        var settings = MongoClientSettings.FromConnectionString(
            Environment.GetEnvironmentVariable(ConnectionStringVariable) ?? DefaultConnectionString
        );
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        settings.SocketTimeout = TimeSpan.FromSeconds(5);

        return new MongoClient(settings).GetDatabase(DatabaseName).GetCollection<BsonDocument>(CollectionName);
    }
}
