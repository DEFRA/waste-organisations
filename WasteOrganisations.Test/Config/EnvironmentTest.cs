using Microsoft.AspNetCore.Builder;

namespace WasteOrganisations.Test.Config;

public class EnvironmentTest
{

   [Fact]
   public void IsNotDevModeByDefault()
   { 
       var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
       var isDev = WasteOrganisations.Config.Environment.IsDevMode(builder);
       Assert.False(isDev);
   }
}
