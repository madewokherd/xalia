namespace Xalia.UiDom
{
    public class UiDomUndefined : UiDomValue
    {
        private UiDomUndefined()
        {

        }

        public static UiDomUndefined Instance = new UiDomUndefined();

        public override string ToString()
        {
            return "undefined";
        }

        public override bool ToBool()
        {
            return false;
        }
    }
}
