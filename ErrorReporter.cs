using System;
using System.Drawing;
using System.Threading;

namespace Maat
{
    // The same as in the compiler - code reuse
    public static class ErrorReporter
    {
        public static bool ThrowExceptions = false;

        // FL = FileAndLine
        public static void ErrorFL(string format, string FileAndLine, params object[] args)
        {
            Console.WriteLine(string.Format("\u001b[1m{0}: \u001b[31merror\u001b[0m\u001b[1m: {1}\u001b[0m",
                FileAndLine, string.Format(format, args)));
            if (ThrowExceptions) throw new Exception();
            Environment.Exit(1);
        }

        public static void WarningFL(string format, string FileAndLine, params object[] args)
        {
            Console.WriteLine(string.Format("\u001b[1m{0}: \u001b[33warning\u001b[0m\u001b[1m: {1}\u001b[0m",
                FileAndLine, string.Format(format, args)));
        }

        public static void NoteFL(string format, string FileAndLine, params object[] args)
        {
            Console.WriteLine(string.Format("\u001b[1m{0}: \u001b[34;1mnote\u001b[0m\u001b[1m: {1}\u001b[0m",
                FileAndLine, string.Format(format, args)));
        }

        public static void Error(string format, params object[] args)
        {
            Console.WriteLine(string.Format("\u001b[1m\u001b[31merror\u001b[0m\u001b[1m: {0}\u001b[0m", string.Format(format, args)));
            if (ThrowExceptions) throw new Exception();
            Environment.Exit(1);
        }

        public static void Warning(string format, params object[] args)
        {
            Console.WriteLine(string.Format("\u001b[1m\u001b[33warning\u001b[0m\u001b[1m: {0}\u001b[0m", string.Format(format, args)));
        }

        public static void Note(string format, params object[] args)
        {
            Console.WriteLine(string.Format("\u001b[1m\u001b[34;1mnote\u001b[0m\u001b[1m: {0}\u001b[0m", string.Format(format, args)));
        }
    }
}
