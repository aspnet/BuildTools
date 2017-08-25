using System;
using System.Reflection;
using Xunit;

namespace TestProject1
{
    public class TestClass1
    {
        [Fact]
        public void HasVersion220()
        {
#if NETCOREAPP1_1
            var xunitVersion = typeof(FactAttribute).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
#elif NETCOREAPP2_0
            var xunitVersion = typeof(FactAttribute).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
#else
#error Update target frameworks
#endif
            Assert.Equal("2.2.0", xunitVersion);
        }

        // Even though the TestLineup includes Moq, the project does not have a PackageReference to moq.
        // This ensures that Moq is not installed into the project
        [Fact]
        public void DoesNotHaveMoq()
        {
            Assert.Null(Type.GetType("Moq.Mock, Moq, Culture=neutral, PublicKeyToken=69f491c39445e920"));
        }
    }
}
