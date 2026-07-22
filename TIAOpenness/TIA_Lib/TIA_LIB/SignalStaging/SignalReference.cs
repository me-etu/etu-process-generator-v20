namespace TIA_LIB.SignalStaging
{
    public class SignalReference
    {
        public SignalReference(string value, bool isGlobal)
        {
            Value = value;
            IsGlobal = isGlobal;
        }

        public string Value { get; private set; }
        public bool IsGlobal { get; private set; }
    }
}
