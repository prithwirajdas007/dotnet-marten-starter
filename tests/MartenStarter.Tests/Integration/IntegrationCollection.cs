namespace MartenStarter.Tests.Integration;

// Every class tagged [Collection("Integration")] joins this collection and shares
// a single IntegrationTestFixture instance — one Postgres container for the whole
// integration suite, not one per test class.
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationTestFixture>;
