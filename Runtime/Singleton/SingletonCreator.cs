    using System;
    using System.Reflection;
    using UnityEngine;

public class SingletonCreator
{
    public static T CreateSingleton<T>()
        where T : class, ISingleton
    {
        T instance = default(T);

        ConstructorInfo[] ctors = typeof(T).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
        ConstructorInfo ctor = Array.Find(ctors, c => c.GetParameters().Length == 0);
        if (ctor == null)
        {
            throw new Exception("Non-public ctor() not found! in" + typeof(T));
        }

        instance = ctor.Invoke(null) as T;
        instance.OnSingletonInit();

        return instance;
    }

    public static T CreateMonoSingleton<T>()
        where T : MonoBehaviour, ISingleton
    {
        T instance = default(T);

        if (instance == null && Application.isPlaying)
        {
            instance = GameObject.FindObjectOfType(typeof(T)) as T;
            if (instance == null)
            {
                MemberInfo info = typeof(T);
                object[] attributes = info.GetCustomAttributes(true);
                for (int i = 0; i < attributes.Length; ++i)
                {
                    MonoSingletonPath defineAttri = attributes[i] as MonoSingletonPath;
                    if (defineAttri == null)
                    {
                        continue;
                    }

                    instance = CreateComponentOnGameObject<T>(defineAttri.PathInHierarchy, true);
                    break;
                }

                if (instance == null)
                {
                    GameObject obj = new GameObject("Singleton of " + typeof(T).Name);
                    instance = obj.AddComponent<T>();
                }
            }

            instance.OnSingletonInit();
        }

        return instance;
    }

    protected static T CreateComponentOnGameObject<T>(string path, bool dontDestory)
        where T : MonoBehaviour
    {
        GameObject obj = FindGameObject(null, path, true, dontDestory);
        if (obj == null)
        {
            obj = new GameObject("Singleton of " + typeof(T).Name);
            if (dontDestory)
            {
                UnityEngine.Object.DontDestroyOnLoad(obj);
            }
        }

        return obj.AddComponent<T>();
    }

    static GameObject FindGameObject(GameObject root, string path, bool build, bool dontDestroy)
    {
        if (path == null || path.Length == 0)
        {
            return null;
        }

        string[] subPath = path.Split('/');
        if (subPath == null || subPath.Length == 0)
        {
            return null;
        }

        return FindGameObject(null, subPath, 0, build, dontDestroy);
    }

    static GameObject FindGameObject(GameObject root, string[] subPath, int index, bool build, bool dontDestroy)
    {
        GameObject go = null;

        if (root == null)
        {
            go = GameObject.Find(subPath[index]);
        }
        else
        {
            var child = root.transform.Find(subPath[index]);
            if (child != null)
            {
                go = child.gameObject;
            }
        }

        if (go == null)
        {
            if (build)
            {
                go = new GameObject(subPath[index]);
                if (root != null)
                {
                    go.transform.SetParent(root.transform);
                }
                if (dontDestroy && index == 0)
                {
                    GameObject.DontDestroyOnLoad(go);
                }
            }
        }

        if (go == null)
        {
            return null;
        }

        if (++index >= subPath.Length)
        {
            return go;
        }

        return FindGameObject(go, subPath, index, build, dontDestroy);
    }
}
