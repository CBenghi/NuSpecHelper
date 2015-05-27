using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace NuSpecHelper
{
    internal class RichTextBoxReporter : IReporter
    {
        private readonly RichTextBox _textBox;

        public RichTextBoxReporter(RichTextBox textBox)
        {
            _textBox = textBox;
        }

        public void AppendLine(string text, System.Windows.Media.Brush brush = null)
        {
            var p = new Paragraph(new Run(text)) {Foreground = brush ?? _textBox.Foreground};
            _textBox.Document.Blocks.Add(p);
        }

    }
}
