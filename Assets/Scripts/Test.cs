using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Test : MonoBehaviour
{

    public Shader shader;

        private void OnEnable()
        {
            CommandBuffer buf = new CommandBuffer();
            buf.DrawRenderer(GetComponent<Renderer>(), new Material(shader), 0,0);
            Camera.main.AddCommandBuffer(CameraEvent.BeforeSkybox, buf);

        }

    // Update is called once per frame
    void Update()
    {
        
    }
}
