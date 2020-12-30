namespace LFAsset.Runtime
{
    public class Reference
    {
        private int refCount;

        public bool IsUnused()
        {
            return refCount <= 0;
        }

        public virtual void Retain()
        {
            refCount++;
        }

        public virtual void Release()
        {
            refCount--;
        }
    }
}
