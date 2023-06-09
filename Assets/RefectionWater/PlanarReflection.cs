using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PlanarReflection
{
    public class PlanarReflection : MonoBehaviour
    {
        [Header("目标平面")]
        [SerializeField]
        private MeshRenderer _targetPlaneRendererer;

        
        [Header("目标贴图横轴")] 
        [SerializeField] private int _targetTextureWidth = 1024;

        [Header("贴图名")] 
        [SerializeField] private string _reflectTexName = "_MirrorReflectTex";

        [Header("采样缩放")]
        [SerializeField]
        private float _scale = 1.0f;

        // 斜切面板的高度
        [HideInInspector]
        public float _clipPlaneOffset = 0.01f;

        public LayerMask reflectMask;
        
        private int _reflectTexInt;

        private Vector3 _planeNormal;
        private Vector3 _planePosition;
        // 法线的transform
        private Transform _normalTrans;
        
        
        private Camera _mainCamera;
        private Transform _mainCameraTrans;
        
        // 反射的相机
        private Camera _reflectionCamera;
        // 反射的transform
        private Transform _reflectionTrans;
        // 法线

        #region 记录的参数
        // 反射平面
        Vector4 _reflectionPlane;
        

        #endregion

        #region 渲染用，临时的 
        
        [SerializeField]
        private RenderTexture _targetTexture;
        #endregion

        private void Awake()
        {

            _mainCamera = Camera.main;
            _mainCameraTrans = _mainCamera.transform;
            
            var go = new GameObject("ReflectionCamera");
            _reflectionCamera = go.AddComponent<Camera>();
            _reflectionCamera.aspect = _mainCamera.aspect;
            _reflectionCamera.fieldOfView = _mainCamera.fieldOfView;
            _reflectionCamera.enabled = false;
            _reflectionCamera.depth = -10;
            // go.hideFlags = HideFlags.HideAndDontSave;
            
            var cameraData = go.AddComponent(typeof(UniversalAdditionalCameraData)) as UniversalAdditionalCameraData;

            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            cameraData.SetRenderer(0);
            
            _reflectionTrans = _reflectionCamera.transform;


            int newWidth = (int)(_targetTextureWidth * _scale);
            _targetTexture = new RenderTexture(newWidth, (int) (newWidth * ((float)Screen.height / Screen.width)), 24);
            //_targetTexture.antiAliasing = 4;
            _targetTexture.format = RenderTextureFormat.ARGB32;
            _reflectionCamera.targetTexture = _targetTexture;
            _reflectionCamera.cullingMask = reflectMask.value;

            
            
            _normalTrans = new GameObject("normal").transform;
            var planeTrans = _targetPlaneRendererer.transform;
            _normalTrans.position = planeTrans.position;
            _normalTrans.rotation = planeTrans.rotation;
            _normalTrans.SetParent(planeTrans);
            _planePosition = _normalTrans.position;
            _planeNormal = _normalTrans.up;
            _reflectionCamera.transform.SetParent(_normalTrans);
            
            
            _reflectTexInt = Shader.PropertyToID(_reflectTexName);
            Shader.SetGlobalTexture(_reflectTexName, _targetTexture);
            
            

        }

        private void OnDestroy()
        {
            
        }


        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }
        
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }
        
        private static void CalReflectionMatrix(ref Matrix4x4 reflectionMat, ref Vector4 plane)
        {
            reflectionMat.m00 = (1F - 2F * plane[0] * plane[0]);
            reflectionMat.m01 = (-2F * plane[0] * plane[1]);
            reflectionMat.m02 = (-2F * plane[0] * plane[2]);
            reflectionMat.m03 = (-2F * plane[3] * plane[0]);

            reflectionMat.m10 = (-2F * plane[1] * plane[0]);
            reflectionMat.m11 = (1F - 2F * plane[1] * plane[1]);
            reflectionMat.m12 = (-2F * plane[1] * plane[2]);
            reflectionMat.m13 = (-2F * plane[3] * plane[1]);

            reflectionMat.m20 = (-2F * plane[2] * plane[0]);
            reflectionMat.m21 = (-2F * plane[2] * plane[1]);
            reflectionMat.m22 = (1F - 2F * plane[2] * plane[2]);
            reflectionMat.m23 = (-2F * plane[3] * plane[2]);

            reflectionMat.m30 = 0F;
            reflectionMat.m31 = 0F;
            reflectionMat.m32 = 0F;
            reflectionMat.m33 = 1F;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderCamera)
        {

            if (renderCamera.cameraType == CameraType.Preview || renderCamera.cameraType == CameraType.Reflection)
            {
                return;
            }

            // 判断相机是不是在法线下方，如果在下方就不做渲染了
            Vector3 localPos = _normalTrans.worldToLocalMatrix.MultiplyPoint3x4(_mainCameraTrans.position);
            if (localPos.y < 0)
            {
                return;
            }
            // 调整位置。
            // 首先计算反射矩阵
            // 法线 
            Vector3 normal = _normalTrans.up;
            // 平面上一个点的位置
            Vector3 pos = _normalTrans.position;
            
            // 获取反射面
            float d = -Vector3.Dot(normal, pos) - _clipPlaneOffset;
            _reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);
            var reflection = Matrix4x4.identity;
            reflection *= Matrix4x4.Scale(new Vector3(1, -1, 1));
            // 计算反射矩阵
            CalReflectionMatrix(ref reflection, ref _reflectionPlane);
            
            // 直接计算世界到相机矩阵
            Matrix4x4 worldToCameraMatrix = _mainCamera.worldToCameraMatrix * reflection;
            _reflectionCamera.worldToCameraMatrix = worldToCameraMatrix;
            
            _reflectionTrans.eulerAngles = _mainCameraTrans.eulerAngles;
            Vector3 localEuler = _reflectionTrans.localEulerAngles;
            localEuler.x *= -1;
            localEuler.z *= -1;
            localPos.y *= -1;
            _reflectionTrans.localEulerAngles = localEuler;
            _reflectionTrans.localPosition = localPos;
            
            // 计算相机空间下的斜切平面
            Vector3 offsetPos = pos + normal * _clipPlaneOffset;
            Vector3 cpos = worldToCameraMatrix.MultiplyPoint3x4(offsetPos);
            Vector3 cnormal = worldToCameraMatrix.MultiplyVector(normal).normalized;
            // 通过斜切面算投影矩阵
            Vector4 clipPlane = new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
            _reflectionCamera.projectionMatrix = _mainCamera.CalculateObliqueMatrix(clipPlane);

            GL.invertCulling = true;
            UniversalRenderPipeline.RenderSingleCamera(context, _reflectionCamera);
            GL.invertCulling = false;
            Shader.SetGlobalTexture(_reflectTexName, _targetTexture);
        }

    }
    
    
}