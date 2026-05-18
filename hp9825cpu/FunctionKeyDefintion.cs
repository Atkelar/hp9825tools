namespace HP9825CPU
{
    public class FunctionKeyDefintion
    {
        public FunctionKeyDefintion(int keyIndex, string text)
        {
            Key = keyIndex;
            Text = text;
        }

        public int Key { get; set; }
        public string Text { get; set; }
    }
}