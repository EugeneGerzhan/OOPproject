using System;
using Infra.Display;
using OOPTextProject.TextSystem;

class Program
{
    static void Main()
    {
        IDisplayer displayer = new ConsoleDisplayer();

        var doc = new TextFactory("Test Document");
        doc.AddParagraph("Some thoughts...");
        doc.AddHeading("Top:");
        doc.AddParagraph("This is a line.");
        doc.AddParagraph("This is another line.");
        doc.AddHeading("Inner:");
        doc.AddParagraph("This is an inner line.");
        doc.Up();
        doc.AddParagraph("Some closure...");

        displayer.Display(doc.GetWholeView(true));
    }
}