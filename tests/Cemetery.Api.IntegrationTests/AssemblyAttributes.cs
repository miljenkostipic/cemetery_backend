using Cemetery.Api.IntegrationTests;

// One shared database: classes in a single collection run one at a time.
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
[assembly: AssemblyFixture(typeof(ApiFixture))]
