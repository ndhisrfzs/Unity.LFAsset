    using UnityEngine;
public abstract class MonoSingletonProperty<T>
    where T : MonoBehaviour, ISingleton
{
    protected static T m_Instance = null;
    public static T Instance
    {
        get
        {
            if (m_Instance == null)
            {
                m_Instance = SingletonCreator.CreateMonoSingleton<T>();
            }
            return m_Instance;
        }
    }

    public static void Dispose()
    {
        Object.Destroy(m_Instance.gameObject);
        m_Instance = null;
    }
}
