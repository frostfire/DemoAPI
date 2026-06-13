using Xunit;

// Each test class spins up its own WebApplicationFactory, and Hangfire keeps
// process-global static state (JobStorage.Current). Running the factories in
// parallel lets them clobber each other's storage, so the integration suite
// runs serially. Unit and architecture tests live in separate assemblies and
// still parallelize.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
