using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CommandLineUtils;

namespace makefont
{
    [Process("mf", "MakeFont", HelpMessage = "The makefont utility is for creating a set of SVG shapes that can be combined into an SVG based font. Source is a text file with 5x7 dot matrix of the characters 0-127. 128-255 are the inverted versions, bit 8 is used for 'overwrite cursor' or inverted text formatting.")]
    public class MakeFontCommand
        : ProcessBase
    {
        private ReturnCodeGroup<FontError> Errors;

        protected override bool BuildReturnCodes(ReturnCodeHandler reg)
        {
            base.BuildReturnCodes(reg);
            Errors = reg.Register<FontError>();
            return true;
        }

        protected override void BuildArguments(ParameterHandler builder)
        {
            Options = builder.AddOptions<FontMakerParameters>();
        }
        protected override async Task RunNow()
        {
            if (!File.Exists(Options.InputFile))
                throw Errors.Happened(FontError.FileNotFound, Options.InputFile);
            string? copyright = null;
            string? name = null;
            int width = 5;
            int height = 7;
            StringBuilder remarks = new StringBuilder();

            List<GlyphDef> defs = new List<GlyphDef>();
            GlyphDef? currentGlyph = null;
            int lastGlyphIndex = -1;

            if (File.Exists(Options.OuptutFile) && !Options.Overwrite)
                throw Errors.Happened(FontError.FileExists, Options.OuptutFile);

            using(var input = new DefFileReader(Options.InputFile, Errors))
            {
                string? line;
                while ((line = await input.ReadLine(currentGlyph != null)) != null)
                {
                    var tLine = line.Trim();
                    if (tLine.StartsWith('.'))
                    {
                        var directive = SplitDirective(tLine);
                        switch(directive[0])
                        {
                            case "copy":
                                copyright = directive.Length>1 ? directive[1] : null;
                                break;
                            case "name":
                                name = directive.Length>1 ? directive[1] : null;
                                break;
                            case "rem":
                                remarks.AppendLine(directive.Length> 1 ? directive[1] : string.Empty);
                                break;
                            case "format":
                                if(directive.Length<2)
                                    throw input.Happened(FontError.InvalidFormat, "<missing details>");
                                if (currentGlyph != null || defs.Count>0)
                                    throw input.Happened(FontError.InvalidFormat, "FORMAT is not allowed after the first glyph is started!");
                                if (!ParseFormat(directive[1], out width, out height))
                                    throw input.Happened(FontError.InvalidFormat, "FORMAT is not in the expected format, expected #x# got " + directive[1]);
                                if (width<1 || width > 32 || height < 1 || height > 32)
                                    throw input.Happened(FontError.InvalidFormat, $"FORMAT is out of range. 1-32 for both sides limit, got {width} by {height}!");
                                break;
                            default: 
                                throw input.Happened(FontError.InvalidDirective, directive[0]);
                        }
                    }
                    else
                    {
                        if (tLine.StartsWith("DEF"))
                        {
                            int idx;
                            char? disp = null;
                            if (tLine.Length>3)
                            {
                                // must have at least char index...
                                if (!char.IsWhiteSpace(tLine[3]))
                                    throw input.Happened(FontError.InvalidDef, "Expected whitespace after DEF.", tLine);
                                tLine = tLine.Substring(3).Trim();
                                int sepIdx = tLine.IndexOfAny(Separators);
                                if (sepIdx > 0)
                                {
                                    // ...can also have display char...
                                    string numVal = tLine.Substring(0, sepIdx);
                                    string charDisp = tLine.Substring(sepIdx+1).Trim();
                                    if (!int.TryParse(numVal, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out idx))
                                        throw input.Happened(FontError.InvalidDef, "Expected char index!", numVal);
                                    if (charDisp.Length!=1)
                                        throw input.Happened(FontError.InvalidDef, "Character display invlid. Must be one char!", charDisp);
                                    disp = charDisp[0];
                                }
                                else
                                {
                                    if (!int.TryParse(tLine, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out idx))
                                        throw input.Happened(FontError.InvalidDef, "Expected char index!", tLine);
                                }
                            }
                            else
                            {
                                // DEF on its own... assume char index AND display char.
                                idx = lastGlyphIndex + 1;
                            }
                            if (idx < 0 || idx > 255)
                                throw input.Happened(FontError.InvalidDef, "Char index out of range!", idx);
                            if (!disp.HasValue)
                            {
                                if (idx == 0 || idx == 127)
                                    disp = ' ';
                                else
                                    disp = (char)idx;
                            }
                            if (defs.Any(x=>x.Index == idx))
                                throw input.Happened(FontError.InvalidDef, "Duplicate glyph definition!", idx);
                            defs.Add(currentGlyph = new GlyphDef(width, height, idx, disp.Value));
                            lastGlyphIndex = idx;
                        }
                        else
                        {
                            // non-empty, non-comment line. MUST be part of a glyph...
                            if (currentGlyph == null)
                                throw input.Happened(FontError.UnexpectedGlyphData, "No DEF for the glyph data!");
                            switch(currentGlyph.AppendLine(line))
                            {
                                case LineParseResult.OK:
                                    break;
                                case LineParseResult.TooManyLines:
                                    // ignore if line is empty...
                                    if (!string.IsNullOrWhiteSpace(line))
                                        throw input.Happened(FontError.GlyphSizeMismatch, $"Too many lines for the glyph: {currentGlyph.Index} ({currentGlyph.DisplayChar})");
                                    break;
                                case  LineParseResult.TooWide:
                                    throw input.Happened(FontError.GlyphSizeMismatch, $"Too many columns for the glyph: {currentGlyph.Index} ({currentGlyph.DisplayChar})");
                                default:
                                    throw input.Happened(FontError.GlyphSizeMismatch, $"Unknown error in glyph parsing: {currentGlyph.Index} ({currentGlyph.DisplayChar})");
                            }
                        }
                    }
                }
            }

            if (defs.Count == 0)
                throw Errors.Happened(FontError.NoGlyphs);

            Out!.WriteLine(VerbosityLevel.Normal, SplitMode.None, "Found {0} glyphs, crating font...", defs.Count);

            if (Options.InvertChars)
            {
                // if (defs.Any(x=>x.Index>127))
                //     throw Errors.Happened(FontError.InvertNotPossible);
                foreach(var thisChar in defs.ToList())  // create copy, we modify the list...
                {
                    // // TODO: mabye make this a "bold" version of the font?
                    // int newIdx = thisChar.Index+0x80;
                    // char newChar = (char)(((int)thisChar.DisplayChar)+0x80);
                    // var inv = new GlyphDef(width, height, newIdx, newChar);
                    for(int y = 0;y<height;y++)
                    {
                        for(int x = 0;x<width;x++)
                            thisChar.SetPix(x,y,!thisChar.GetPix(x,y));
                            //inv.SetPix(x,y,!thisChar.GetPix(x,y));
                    }
                    // defs.Add(inv);
                }
            }

            using (var tw = File.CreateText(Options.OuptutFile))
            {
                double emSquare = Options.ConformSize;
                double sFactor = Options.ConformSize / (height * Options.PixelSize + (height - 1) * Options.DotSpacing);
                double pFactor = sFactor * (Options.PixelSize + Options.DotSpacing);
                double pSize = Options.PixelSize * sFactor;

                double gWidth = width * pFactor;
                double gHeight = height * pFactor;
                tw.WriteLine("<?xml version='1.0'?>");
                tw.WriteLine("<svg xmlns='http://www.w3.org/2000/svg'>", gWidth, gHeight);  // width='{0}' height='{1}' 
                tw.WriteLine("<metadata>");
                tw.WriteLine(" <generated>Atkelar's HP 9825A project font generator, {0:O}.</generated>", DateTime.UtcNow);
                if (copyright != null)
                    tw.WriteLine(" <copyright>{0}</copyright>", System.Uri.EscapeDataString(copyright));
                if (remarks.Length>0)
                    tw.WriteLine(" <remarks>{0}</remarks>", System.Uri.EscapeDataString(remarks.ToString()));
                tw.WriteLine("</metadata>");
                tw.WriteLine("<defs>");
                tw.WriteLine("<font id='{0}' >", Options.FontId);
                tw.WriteLine("<font-face units-per-em='{0:0}' cap-height='{0:0}' ascent='{0:0}' descent='0' font-family='{1}' font-weight='{2}' />", 
                    emSquare, name ?? "HP 9825A", Options.InvertChars ? "bold" : "normal");
                tw.WriteLine("<missing-glyph horiz-adv-x='{0}'/>", (width + 1) * pFactor);

                foreach(var thisChar in defs)
                {
                    StringBuilder dBuilder = new StringBuilder();
                    for(int y = 0;y < height;y++)
                    {
                        for(int x=0;x<width;x++)
                        {
                            if (thisChar.GetPix(x,y))
                            {
                                double pX, pY;
                                pX = x * pFactor;
                                pY = gHeight + (y+1) * -pFactor;    // need to get to baseline (=0)
                                dBuilder.AppendFormat("M{0:0},{1:0}", pX, pY);
                                dBuilder.AppendFormat("v{0:0}h{0:0}v-{0:0}Z", pSize);
                                //tw.WriteLine("<rect width='{0}' height='{0}' x='{1}' y='{2}' fill='black' />", Options.PixelSize, );
                            }
                        }
                    }
                    tw.WriteLine("<glyph unicode='&#x{1:X4};' d='{0}' horiz-adv-x='{2:0}' orientation='h' />", dBuilder.ToString(), (int)thisChar.DisplayChar, (width + 1) * pFactor);
                }
                tw.WriteLine("</font>");
                tw.WriteLine("</defs>");
                tw.WriteLine("</svg>");
            }
        }

        private bool ParseFormat(string v, out int width, out int height)
        {
            width = height = 0;
            var str = v.Split('x', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (str.Length != 2)
                return false;
            if (!int.TryParse(str[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out width))
                return false;
            if (!int.TryParse(str[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out height))
                return false;
            return true;
        }

        private static readonly char[] Separators = " \t".ToCharArray();

        private string[] SplitDirective(string tLine)
        {
            int index = tLine.IndexOfAny(Separators, 1);
            if (index<0)
                return new string[] {tLine.Substring(1).Trim().ToLowerInvariant()};
            else
            {
                return new string[] {
                    tLine.Substring(1, index-1).Trim().ToLowerInvariant(),
                    tLine.Substring(index+1).Trim()
                };
            }
        }

        FontMakerParameters Options {get;set;}


    }
}