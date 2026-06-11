using System.Reflection;

namespace GridTrack.NamingConventions;

public class NamingConventionTests
{
    protected const string DomainNamespace = "GridTrack.Domain";
    protected const string ApplicationNamespace = "GridTrack.Application";
    protected const string InfrastructureNamespace = "GridTrack.Infrastructure";
    protected const string PresentationNamespace = "GridTrack.Presentation";
    protected const string ApiNamespace = "GridTrack.Api";

    protected static readonly Assembly DomainAssembly = typeof(Domain.Drivers.Driver).Assembly;
    protected static readonly Assembly ApplicationAssembly = typeof(Application.DependencyInjection).Assembly;

}