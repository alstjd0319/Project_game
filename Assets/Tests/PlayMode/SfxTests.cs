using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>코드로 합성한 효과음: 빠진 소리·무음·찢어짐이 없는지, 같은 소리가 한 프레임에 겹쳐 커지지 않는지. 미리 듣기 WAV를 Captures/sfx/에 저장한다.</summary>
    public class SfxTests
    {
        [UnityTest]
        public IEnumerator 모든_효과음이_만들어지고_미리_듣기_파일로_저장된다()
        {
            yield return TestScene.LoadPrototype();
            Assert.IsNotNull(Sfx.Instance);
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Captures", "sfx");
            Directory.CreateDirectory(dir);
            foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
            {
                var clip = Sfx.ClipOf(id);
                Assert.IsNotNull(clip, $"{id} 소리가 있다");
                Assert.Less(clip.length, 2f, $"{id} 길이");
                if (clip.loadType == AudioClipLoadType.DecompressOnLoad && clip.LoadAudioData() && clip.loadState == AudioDataLoadState.Loaded)
                {
                    var data = new float[clip.samples * clip.channels];
                    if (clip.GetData(data, 0))
                    {
                        float peak = 0f;
                        foreach (float v in data)
                        {
                            Assert.IsFalse(float.IsNaN(v), $"{id} NaN");
                            peak = Mathf.Max(peak, Mathf.Abs(v));
                        }
                        Assert.Greater(peak, 0.05f, $"{id} 무음이 아니다");
                        Assert.LessOrEqual(peak, 1f, $"{id} 찢어지지 않는다");
                        Sfx.SaveWav(clip, Path.Combine(dir, $"{id}.wav"));
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator 스킬마다_다른_소리이고_피격음은_무게별로_다르다()
        {
            yield return TestScene.LoadPrototype();
            var skills = new[] { SfxId.StaffSwing1, SfxId.StaffSwing2, SfxId.StaffSwing3, SfxId.TalismanThrow, SfxId.SkillSweep, SfxId.SkillSlam, SfxId.SkillFan, SfxId.SkillSeal };
            var hits = new[] { SfxId.HitLight, SfxId.EnemyHit, SfxId.HitHeavy, SfxId.HitMetal, SfxId.Hurt, SfxId.HurtHeavy };
            foreach (var group in new[] { skills, hits })
                for (int i = 0; i < group.Length; i++)
                    for (int j = i + 1; j < group.Length; j++)
                        Assert.AreNotSame(Sfx.ClipOf(group[i]), Sfx.ClipOf(group[j]), $"{group[i]} ≠ {group[j]}");
        }
    }
}
