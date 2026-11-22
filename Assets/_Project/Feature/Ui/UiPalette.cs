using UnityEngine;

namespace MissedCall.Feature.Ui {
    /// <summary>UI 색. game-design.md의 색 표와 일치시킨다.</summary>
    public static class UiPalette {
        public static readonly Color Night = Hex(0x10161C);
        public static readonly Color Glass = new(Night.r, Night.g, Night.b, 0.84f);
        public static readonly Color Paper = Hex(0xF3EEE3);
        public static readonly Color Muted = Hex(0xB5B3A8);
        public static readonly Color Ring = Hex(0xFFD23F);
        public static readonly Color Miss = Hex(0xFF6B57);
        public static readonly Color Answer = Hex(0x57D68D);
        public static readonly Color Frame = Hex(0x39424B);
        /// <summary>보조 버튼 바탕 (Restart, Title). 반투명 흰색</summary>
        // 선형 색 공간이라 흰색을 조금만 섞어도 밝게 보인다.
        public static readonly Color Secondary = new(1f, 1f, 1f, 0.06f);
        public static readonly Color SecondaryHover = new(1f, 1f, 1f, 0.13f);

        private static Color Hex(int rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
