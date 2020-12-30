using System;

[AttributeUsage(AttributeTargets.Class)]
public class MonoSingletonPath : Attribute
{
    private string m_PathInHierarchy;
    public MonoSingletonPath(string pathInHierarchy)
    {
        m_PathInHierarchy = pathInHierarchy;
    }

    public string PathInHierarchy
    {
        get
        {
            return m_PathInHierarchy;
        }
    }
}
