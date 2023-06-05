using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
class TestShaderParams : MonoBehaviour
{
    private int shaderReflectionAngleHash;
    private Image imageComponent;
    private float shaderReflexVal = 0.0f;

    private void Start()
    {
        shaderReflectionAngleHash = Shader.PropertyToID( "_ReflectionAngle" );
        imageComponent = GetComponent<Image>();
    }

    private void Update()
    {
        shaderReflexVal += Time.deltaTime;
        imageComponent.material.SetFloat( shaderReflectionAngleHash, shaderReflexVal );
    }

    void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if( !Application.isPlaying )
        {
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }
}

