using UnityEngine;
using UnityEngine.UI;

namespace AfterYou.UI
{
    /// <summary>
    /// Text의 숫자 글리프를 균일 폭 슬롯에 재배치하는 메시 이펙트.
    /// 비고정폭 폰트(Bazzi 숫자 폭 12~20px)에서 값이 바뀔 때마다 문자열 전체 폭이 변해
    /// 중앙 정렬 텍스트가 좌우로 흔들리는 문제를 원천 차단한다. 단일 행 값 표시 Text 전용.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class FixedDigitWidth : BaseMeshEffect
    {
        private const string Digits = "0123456789";

        private Text _text;

        private Text TargetText
        {
            get
            {
                if (_text == null)
                {
                    _text = GetComponent<Text>();
                }
                return _text;
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
            {
                return;
            }

            var text = TargetText;
            string content = text.text;
            if (string.IsNullOrEmpty(content) || content.IndexOf('\n') >= 0)
            {
                return;
            }
            // 공백은 쿼드를 만들지 않는다(실측: "1 / 1" = 5글자 12정점) — 비공백 수 기준으로
            // 문자↔쿼드 매핑을 검사하고, 깨지는 경우(리치 텍스트, 잘림 등)는 건드리지 않는다
            int visibleCount = 0;
            for (int i = 0; i < content.Length; i++)
            {
                if (!char.IsWhiteSpace(content[i]))
                {
                    visibleCount++;
                }
            }
            if (visibleCount == 0 || vh.currentVertCount != visibleCount * 4)
            {
                return;
            }

            var characters = text.cachedTextGenerator.characters;
            if (characters.Count < content.Length)
            {
                return;
            }

            // 슬롯 폭 = 폰트의 전체 숫자 중 최대 advance. 현재 문자열에 없는 숫자('4' 등)가
            // 나중에 나타나도 폭이 일정해야 하므로 반드시 0~9 전수 최대값을 쓴다
            var font = text.font;
            font.RequestCharactersInTexture(Digits, text.fontSize, text.fontStyle);
            int maxAdvance = 0;
            foreach (char d in Digits)
            {
                CharacterInfo digitInfo;
                if (font.GetCharacterInfo(d, out digitInfo, text.fontSize, text.fontStyle))
                {
                    maxAdvance = Mathf.Max(maxAdvance, digitInfo.advance);
                }
            }
            if (maxAdvance <= 0)
            {
                return;
            }

            // 폰트 메트릭(fontSize 기준 px) → 제너레이터 공간 환산 비율.
            // 캔버스 scaleFactor에 따라 제너레이터가 다른 크기로 굽기 때문에, 현재 문자열의
            // 숫자 하나로 실측 비율을 얻는다 (숫자가 없으면 재배치할 것도 없으니 종료)
            float ratio = -1f;
            for (int i = 0; i < content.Length; i++)
            {
                CharacterInfo info;
                if (char.IsDigit(content[i])
                    && font.GetCharacterInfo(content[i], out info, text.fontSize, text.fontStyle)
                    && info.advance > 0)
                {
                    ratio = characters[i].charWidth / info.advance;
                    break;
                }
            }
            if (ratio <= 0f)
            {
                return;
            }

            float slot = maxAdvance * ratio;

            // 새 pen 위치 계산: 숫자는 슬롯 폭, 그 외 문자는 원래 advance 유지
            int count = content.Length;
            float widthOld = 0f;
            float pen = 0f;
            var newPen = new float[count];
            for (int i = 0; i < count; i++)
            {
                newPen[i] = pen;
                float advance = characters[i].charWidth;
                pen += char.IsDigit(content[i]) ? slot : advance;
                widthOld += advance;
            }
            float widthNew = pen;

            // 정렬 유지 보정: 중앙 정렬이면 폭 변화의 절반을 되돌려 시각적 중앙을 고정.
            // 숫자 개수가 같으면 widthNew가 상수가 되므로 값이 바뀌어도 전체가 흔들리지 않는다
            float baseShift = -(widthNew - widthOld) * GetHorizontalAnchorFactor(text.alignment);

            float unitsPerPixel = 1f / text.pixelsPerUnit;
            float firstPen = characters[0].cursorPos.x;
            var vertex = new UIVertex();
            int quadIndex = 0; // 공백은 쿼드가 없으므로 비공백 문자만 순서대로 대응
            for (int i = 0; i < count; i++)
            {
                if (char.IsWhiteSpace(content[i]))
                {
                    continue;
                }
                float oldPen = characters[i].cursorPos.x - firstPen;
                float centering = char.IsDigit(content[i]) ? (slot - characters[i].charWidth) * 0.5f : 0f;
                float dx = (newPen[i] + centering - oldPen + baseShift) * unitsPerPixel;
                if (!Mathf.Approximately(dx, 0f))
                {
                    for (int v = quadIndex * 4; v < quadIndex * 4 + 4; v++)
                    {
                        vh.PopulateUIVertex(ref vertex, v);
                        vertex.position.x += dx;
                        vh.SetUIVertex(vertex, v);
                    }
                }
                quadIndex++;
            }
        }

        private static float GetHorizontalAnchorFactor(TextAnchor alignment)
        {
            switch (alignment)
            {
                case TextAnchor.UpperLeft:
                case TextAnchor.MiddleLeft:
                case TextAnchor.LowerLeft:
                    return 0f;
                case TextAnchor.UpperRight:
                case TextAnchor.MiddleRight:
                case TextAnchor.LowerRight:
                    return 1f;
                default:
                    return 0.5f;
            }
        }
    }
}
