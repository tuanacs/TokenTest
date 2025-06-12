using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(TokenTest.Startup))]
namespace TokenTest
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
        }
    }
}
