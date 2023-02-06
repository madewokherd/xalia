namespace Xalia.UiDom
{
    internal class UiDomBoolean : UiDomValue
    {
        private UiDomBoolean(bool value)
        {
            Value = value;
            if (value)
                stringval = "true";
            else
                stringval = "false";
        }

        public static UiDomBoolean True = new UiDomBoolean(true);
        public static UiDomBoolean False = new UiDomBoolean(false);

        public static UiDomBoolean FromBool(bool value) => value ? True : False;

        string stringval;
        
        public bool Value { get; }

        public override string ToString()
        {
            return stringval;
        }

        public override bool ToBool()
        {
            return Value;
        }
    }
}
