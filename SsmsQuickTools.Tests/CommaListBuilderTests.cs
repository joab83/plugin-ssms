using System;
using System.Collections.Generic;
using SsmsQuickTools.Features.CommaList;
using Xunit;

namespace SsmsQuickTools.Tests
{
    public class CommaListBuilderTests
    {
        private static string Build(string[] columns, params string[][] rows)
            => CommaListBuilder.Build(columns, rows);

        [Fact]
        public void Build_Enteros_SinComillas()
        {
            var text = Build(new[] { "Id" }, new[] { "10" }, new[] { "20" }, new[] { "30" });

            Assert.Equal("10, 20, 30", text);
        }

        [Fact]
        public void Build_Texto_ConNPrefijo()
        {
            var text = Build(new[] { "Nombre" }, new[] { "a" }, new[] { "b" });

            Assert.Equal("N'a', N'b'", text);
        }

        [Fact]
        public void Build_ColumnaMixta_TodaComoTexto()
        {
            var text = Build(new[] { "X" }, new[] { "10" }, new[] { "abc" });

            Assert.Equal("N'10', N'abc'", text);
        }

        [Fact]
        public void Build_ComillaSimple_SeEscapa()
        {
            var text = Build(new[] { "Nombre" }, new[] { "O'Brien" });

            Assert.Equal("N'O''Brien'", text);
        }

        [Fact]
        public void Build_Null_SeOmite()
        {
            var text = Build(new[] { "Id" }, new[] { "10" }, new string[] { null }, new[] { "NULL" }, new[] { "20" });

            Assert.Equal("10, 20", text);
        }

        [Fact]
        public void Build_Duplicados_SeQuitanConservandoOrden()
        {
            var text = Build(new[] { "Id" }, new[] { "30" }, new[] { "10" }, new[] { "30" }, new[] { "20" }, new[] { "10" });

            Assert.Equal("30, 10, 20", text);
        }

        [Fact]
        public void Build_DosColumnas_UnaLineaPorColumna()
        {
            var text = Build(new[] { "Id", "Nombre" }, new[] { "10", "a" }, new[] { "20", "b" });

            Assert.Equal("10, 20" + Environment.NewLine + "N'a', N'b'", text);
        }

        [Fact]
        public void Build_ColumnaTodaNull_LineaVacia()
        {
            var text = Build(new[] { "A", "B" }, new[] { "1", null }, new[] { "2", null });

            Assert.Equal("1, 2" + Environment.NewLine, text);
        }

        [Fact]
        public void Build_Guid_ConComillasSinN()
        {
            var text = Build(new[] { "G" }, new[] { "6F9619FF-8B86-D011-B42D-00C04FC964FF" });

            Assert.Equal("'6F9619FF-8B86-D011-B42D-00C04FC964FF'", text);
        }

        [Fact]
        public void Build_SinColumnas_Lanza()
        {
            Assert.Throws<ArgumentException>(() => CommaListBuilder.Build(new List<string>(), new List<string[]>()));
        }
    }
}
