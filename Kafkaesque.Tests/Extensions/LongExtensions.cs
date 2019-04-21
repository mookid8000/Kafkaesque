namespace Kafkaesque.Tests.Extensions
{
    static class LongExtensions
    {
        public static string FormatAsHumanReadableSize(this long byteCount)
        {
            if (byteCount < 1024) return $"{byteCount} B";

            var kiloBytes1 = byteCount / (double)1024;

            if (kiloBytes1 < 1024) return $"{kiloBytes1:0.0#} kB";

            var megaBytes1 = kiloBytes1 / 1024;

            return $"{megaBytes1:0.0#} MB";
        }
    }
}