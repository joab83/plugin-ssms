using System;
using SsmsQuickTools.Features.TopLevelMenu;
using Xunit;

namespace SsmsQuickTools.Tests
{
    public class AllowedMenuListTests
    {
        private const string Entry = "{2E7F3EAB-6748-4AA6-BDCF-9C69E43A48FD},4352";

        [Fact]
        public void FormatEntry_GuidMayusculasConLlavesEIdDecimal()
        {
            var guid = new Guid("2e7f3eab-6748-4aa6-bdcf-9c69e43a48fd");
            Assert.Equal(Entry, AllowedMenuList.FormatEntry(guid, 0x1100));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void AddEntry_ListaVacia_DevuelveSoloLaEntrada(string existing)
        {
            Assert.Equal(Entry, AllowedMenuList.AddEntry(existing, Entry));
        }

        [Fact]
        public void AddEntry_YaPresenteConOtroCasing_NoCambia()
        {
            string existing = "{2e7f3eab-6748-4aa6-bdcf-9c69e43a48fd},4352";
            Assert.Equal(existing, AllowedMenuList.AddEntry(existing, Entry));
        }

        [Fact]
        public void AddEntry_ConOtrasEntradas_LasConservaYAgregaAlFinal()
        {
            string other = "{11111111-1111-1111-1111-111111111111},1";
            Assert.Equal(other + ";" + Entry, AllowedMenuList.AddEntry(other, Entry));
        }

        [Fact]
        public void AddEntry_SeparadoresSobrantes_SeNormalizan()
        {
            string other = "{11111111-1111-1111-1111-111111111111},1";
            Assert.Equal(other + ";" + Entry, AllowedMenuList.AddEntry(";" + other + ";;", Entry));
        }

        [Fact]
        public void Contains_MismoIdDistintoGuid_EsFalso()
        {
            Assert.False(AllowedMenuList.Contains("{11111111-1111-1111-1111-111111111111},4352", Entry));
        }
    }
}
