using LFAsset.Runtime;

using UnityEngine;

public class Test : MonoBehaviour
{
    private GameObject loginPanel;
    private void Awake()
    {
    }

    private void Start()
    {
        var go = ResMgr.GetResource<GameObject>("Prefabs/Test", "LoginPanel");
        loginPanel = Instantiate(go, this.transform);
    }

    public void OnClickLoad()
    {
        GameObject.DestroyImmediate(loginPanel);
        ResMgr.Unload("Prefabs/Test/LoginPanel");
        var obj = ResMgr.GetResource<GameObject>("Test", "Cube");
        var go = GameObject.Instantiate(obj);

    }
}
