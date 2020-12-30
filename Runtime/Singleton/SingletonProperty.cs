public class SingletonProperty<T>
    where T : class, ISingleton
{
    protected static T m_Instance = null;
    static object m_Lock = new object();

    public static T Instance
    {
        get
        {
            lock (m_Lock)
            {
                if (m_Instance == null)
                {
                    m_Instance = SingletonCreator.CreateSingleton<T>();
                }
            }

            return m_Instance;
        }
    }

    public static void Dispose()
    {
        m_Instance = null;
    }
}
