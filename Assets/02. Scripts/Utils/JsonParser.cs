using System;
using UnityEngine;

namespace TankAttack.Utils
{
    public class JsonParser : MonoBehaviour
    {
        /// <summary>
        /// JSON에서 Vector3 값을 추출하는 유틸리티 메소드
        /// </summary>
        public static Vector3 ExtractVector3Value(string json, string fieldName)
        {
            try
            {
                // 여러 가능한 패턴 시도
                string[] patterns =
                {
                    $"\"{fieldName}\":{{",      // "Position":{"X":1,"Y":0,"Z":0}
                    $"\"{fieldName}\": {{",     // "Position": {"X":1,"Y":0,"Z":0}
                    $"\"{fieldName}\" : {{",    // "Position" : {"X":1,"Y":0,"Z":0}
                    $"\"{fieldName}\":  {{"     // "Position":  {"X":1,"Y":0,"Z":0}
                };

                int startIndex = -1;
                foreach (string pattern in patterns)
                {
                    startIndex = json.IndexOf(pattern);
                    if (startIndex != -1) break;
                }

                if (startIndex == -1)
                {
                    Debug.LogWarning($"[NetworkManager] Vector3 패턴을 찾을 수 없음 - Field: {fieldName}, JSON: {json}");
                    return Vector3.zero;
                }

                // '{' 다음부터 '}' 까지 찾기
                int openBrace = json.IndexOf('{', startIndex);
                if (openBrace == -1)
                {
                    //Debug.LogWarning($"[NetworkManager] 여는 중괄호를 찾을 수 없음 - Field: {fieldName}");
                    return Vector3.zero;
                }

                int closeBrace = json.IndexOf('}', openBrace);
                if (closeBrace == -1)
                {
                    //Debug.LogWarning($"[NetworkManager] 닫는 중괄호를 찾을 수 없음 - Field: {fieldName}");
                    return Vector3.zero;
                }

                string vectorData = json.Substring(openBrace + 1, closeBrace - openBrace - 1);
                //Debug.Log($"[NetworkManager] Vector3 파싱 - Field: {fieldName}, VectorData: '{vectorData}'");

                float x = ExtractFloatFromVector(vectorData, "X");
                float y = ExtractFloatFromVector(vectorData, "Y");
                float z = ExtractFloatFromVector(vectorData, "Z");

                Vector3 result = new Vector3(x, y, z);
                //Debug.Log($"[NetworkManager] Vector3 파싱 완료 - Field: {fieldName}, Result: {result}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] Vector3 파싱 오류 - Field: {fieldName}, Error: {ex.Message}");
                return Vector3.zero;
            }
        }


        /// <summary>
        /// Vector 데이터에서 float 값을 추출하는 유틸리티 메소드
        /// </summary>
        private static float ExtractFloatFromVector(string vectorData, string axis)
        {
            try
            {
                // 여러 가능한 패턴 시도
                string[] patterns =
                {
                    $"\"{axis}\":", // "X":1.5
                    $"\"{axis}\": ", // "X": 1.5
                    $"\"{axis}\" : " // "X" : 1.5
                };

                int startIndex = -1;
                string foundPattern = "";
                foreach (string pattern in patterns)
                {
                    int patternIndex = vectorData.IndexOf(pattern);
                    if (patternIndex != -1)
                    {
                        startIndex = patternIndex + pattern.Length;
                        foundPattern = pattern;
                        break;
                    }
                }

                if (startIndex == -1)
                {
                    //Debug.LogWarning($"[NetworkManager] Float 패턴을 찾을 수 없음 - Axis: {axis}, VectorData: '{vectorData}'");
                    return 0f;
                }

                // 값의 끝 위치 찾기 (콤마나 끝까지)
                int endIndex = vectorData.IndexOf(",", startIndex);
                if (endIndex == -1) endIndex = vectorData.Length;

                string valueStr = vectorData.Substring(startIndex, endIndex - startIndex).Trim();
                //Debug.Log($"[NetworkManager] Float 파싱 - Axis: {axis}, Pattern: '{foundPattern}', Value: '{valueStr}'");

                if (float.TryParse(valueStr, out float result))
                {
                    return result;
                }
                else
                {
                    //Debug.LogWarning($"[NetworkManager] Float 파싱 실패 - Axis: {axis}, Value: '{valueStr}'");
                    return 0f;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] Float 파싱 오류 - Axis: {axis}, Error: {ex.Message}");
                return 0f;
            }
        }
    }
}