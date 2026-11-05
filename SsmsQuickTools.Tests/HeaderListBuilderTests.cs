using SsmsQuickTools.Features.CopyHeaders;
using Xunit;

namespace SsmsQuickTools.Tests
{
    public class HeaderListBuilderTests
    {
        [Fact]
        public void Build_UnaColumna()
        {
            Assert.Equal("[Id]", HeaderListBuilder.Build(new[] { "Id" }));
        }

        [Fact]
        public void Build_VariasColumnas_SeparadasPorComaEspacio()
        {
            Assert.Equal("[Id], [Name], [Created At]", HeaderListBuilder.Build(new[] { "Id", "Name", "Created At" }));
        }

        [Fact]
        public void Build_CorcheteDeCierre_SeDuplica()
        {
            Assert.Equal("[a]]b]", HeaderListBuilder.Build(new[] { "a]b" }));
        }

        [Fact]
        public void Build_CorcheteDeApertura_NoSeEscapa()
        {
            Assert.Equal("[a[b]", HeaderListBuilder.Build(new[] { "a[b" }));
        }

        [Fact]
        public void Build_SinColumnas_CadenaVacia()
        {
            Assert.Equal(string.Empty, HeaderListBuilder.Build(new string[0]));
            Assert.Equal(string.Empty, HeaderListBuilder.Build(null));
        }

        [Fact]
        public void Build_NombreVacioONulo_CorchetesVacios()
        {
            Assert.Equal("[], []", HeaderListBuilder.Build(new[] { "", null }));
        }

        [Fact]
        public void Build_Repetidos_SeCopianTalCual()
        {
            Assert.Equal("[A], [A]", HeaderListBuilder.Build(new[] { "A", "A" }));
        }
    }
}
