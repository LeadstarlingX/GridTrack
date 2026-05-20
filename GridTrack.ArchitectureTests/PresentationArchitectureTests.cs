using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

namespace GridTrack.ArchitectureTests;

public class PresentationArchitectureTests : ArchitectureTest
{
    [Test]
    public async Task Controllers_Should_Inherit_From_ApiController()
    {
        var result = Types.InAssembly(PresentationAssembly)
            .That()
            .ResideInNamespace("GridTrack.Presentation.Controllers")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Controller")
            .And()
            .DoNotHaveName("BaseController") 
            .Should()
            .Inherit(typeof(ControllerBase))
            .GetResult();

        await Assert.That(result.IsSuccessful).IsTrue();
    }

}
