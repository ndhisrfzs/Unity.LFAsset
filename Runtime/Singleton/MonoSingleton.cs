using UnityEngine;
public abstract class MonoSingleton<T> : MonoBehaviour, ISingleton
    where T : MonoSingleton<T>
{
    protected static T m_Ins = null;
    public static T Ins
    {
        get
        {
            if (m_Ins == null)
            {
                m_Ins = SingletonCreator.CreateMonoSingleton<T>();
            }

            return m_Ins;
        }
    }

    public void Dispose()
    {
        Destroy(gameObject);
    }

    protected virtual void OnDestroy()
    {
        m_Ins = null;
    }

    public virtual void OnSingletonInit()
    {
    }
}
