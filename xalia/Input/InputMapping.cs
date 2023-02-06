namespace Xalia.Input
{
    public class InputMapping
    {
        public InputMapping(string name, string pngfilename)
        {
            HumanReadableName = name;
            GlyphPngFilename = pngfilename;
        }

        public InputMapping(string name, string pngfilename, string glyphstring, int x, int y, int height) :
            this(name, pngfilename)
        {
            GlyphString = glyphstring;
            GlyphStringCenterX = x;
            GlyphStringCenterY = y;
            GlyphStringHeight = height;
        }

        public string GlyphPngFilename { get; }
        public string GlyphString { get; }
        public int GlyphStringCenterX { get; }
        public int GlyphStringCenterY { get; }
        public int GlyphStringHeight { get; }

        public string HumanReadableName { get; }
    }
}
