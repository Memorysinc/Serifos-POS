using Xunit;

// WPF Application.Current is a process-wide AppDomain singleton and tests share a live MySQL instance.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
