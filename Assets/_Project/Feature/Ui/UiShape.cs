using UnityEngine;
using UnityEngine.UI;

namespace MissedCall.Feature.Ui {
    /// <summary>
    /// UiHalftone 셰이더로 그리는 도형의 모양 값. Image(스프라이트 없음)에 붙이면 사각형을 잘게 나눈 격자로 바꾸고
    /// 정점에 크기, 모서리 깎기, 기울임, 하프톤, 테두리 값을 넣는다. 부모에 <see cref="UiTilt"/>가 있으면 원근 투영도 한다.
    /// 캔버스의 추가 셰이더 채널(TexCoord1~3)이 켜져 있어야 한다.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class UiShape : BaseMeshEffect {
        /// <summary>하프톤 방향에 넣는 특수 값: 고르게</summary>
        public const float Uniform = 9f;
        /// <summary>하프톤 방향에 넣는 특수 값: 가운데에서 바깥으로 커짐</summary>
        public const float Radial = 8f;

        private const int Columns = 8;
        private const int Rows = 6;

        [Tooltip("왼쪽 위, 오른쪽 아래 모서리를 깎는 크기(px)")]
        public float chamfer;
        [Tooltip("위쪽을 오른쪽으로 미는 기울임(px)")]
        public float skew;
        [Range(0f, 1f)] public float halftone;
        [Tooltip("하프톤이 커지는 방향(도). 9 = 고르게, 8 = 가운데에서 바깥으로")]
        public float halftoneAngle = -45f;
        [Tooltip("점 색. +면 밝은 청회색(_DotLight), -면 검정. 크기가 진하기")]
        [Range(-1f, 1f)] public float dotTone;
        [Tooltip("0보다 크면 이 두께(px)의 테두리만 그린다")]
        public float outline;
        [Tooltip("채우기 불투명도. 0이면 하프톤 점만 보인다")]
        [Range(0f, 1f)] public float fillOpacity = 1f;

        private UiTilt tilt;

        protected override void OnEnable() {
            tilt = GetComponentInParent<UiTilt>();
            base.OnEnable();
        }

        protected override void OnTransformParentChanged() {
            tilt = GetComponentInParent<UiTilt>();
            base.OnTransformParentChanged();
        }

        public override void ModifyMesh(VertexHelper vh) {
            if (!IsActive()) {
                return;
            }

            var rectTransform = (RectTransform)transform;
            Rect rect = rectTransform.rect;
            Color32 color = graphic.color;
            if (vh.currentVertCount > 0) {
                UIVertex first = default;
                vh.PopulateUIVertex(ref first, 0);
                color = first.color;
            }

            vh.Clear();
            float angle = halftoneAngle >= Radial - 0.5f ? halftoneAngle : halftoneAngle * Mathf.Deg2Rad;
            var shape = new Vector4(rect.width, rect.height, chamfer, skew);
            var tone = new Vector4(halftone, angle, dotTone, outline);
            var fill = new Vector4(fillOpacity, 0f, 0f, 0f);
            bool project = tilt != null && tilt.isActiveAndEnabled;

            for (int y = 0; y <= Rows; y++) {
                for (int x = 0; x <= Columns; x++) {
                    var uv = new Vector2(x / (float)Columns, y / (float)Rows);
                    var position = new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, uv.x), Mathf.Lerp(rect.yMin, rect.yMax, uv.y), 0f);
                    if (project) {
                        position = tilt.Project(rectTransform, position);
                    }

                    vh.AddVert(new UIVertex {
                        position = position,
                        color = color,
                        uv0 = uv,
                        uv1 = shape,
                        uv2 = tone,
                        uv3 = fill,
                    });
                }
            }

            for (int y = 0; y < Rows; y++) {
                for (int x = 0; x < Columns; x++) {
                    int i = y * (Columns + 1) + x;
                    vh.AddTriangle(i, i + Columns + 1, i + Columns + 2);
                    vh.AddTriangle(i, i + Columns + 2, i + 1);
                }
            }
        }

        /// <summary>UiTilt가 매 프레임 부른다.</summary>
        public void Refresh() {
            if (graphic != null) {
                graphic.SetVerticesDirty();
            }
        }
    }
}
