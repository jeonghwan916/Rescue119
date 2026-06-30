using System;
using UnityEngine;

namespace FireLink119.Fire
{
    public class FireObject : MonoBehaviour
    {
        private struct ParticleInitialState
        {
            public ParticleSystem Particle;
            public ParticleSystem.MinMaxCurve RateOverTime;
            public ParticleSystem.MinMaxCurve StartSize;
            public ParticleSystem.MinMaxCurve StartLifetime;
        }

        [Header("Particle System")]
        [SerializeField] private ParticleSystem[] _fireParticles;

        [Header("Extinguish : Stages")]
        [SerializeField] private float _extinguishDuration = 4f;
        [SerializeField] private int _extinguishStageCount = 4;
        [SerializeField] private bool _disableWhenExtinguished = true;

        private ParticleInitialState[] _particleInitialStates;
        private float _accumulatedExtinguishTime;
        private int _currentStage;
        private bool _isExtinguished;
        
        // 불이 꺼졌을때의 후속 동작이 필요하다면 여기 이벤트 구독하면 됨
        public event Action OnExtinguished;
        
        // 기존 필드들 아래에 추가
        private AudioSource _audioSource;
        private float _initialVolume;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource != null)
                _initialVolume = _audioSource.volume;

            if (_fireParticles == null || _fireParticles.Length == 0)
                _fireParticles = GetComponentsInChildren<ParticleSystem>();

            _particleInitialStates = new ParticleInitialState[_fireParticles.Length];

            for (int i = 0; i < _fireParticles.Length; i++)
            {
                ParticleSystem ps = _fireParticles[i];
                if (ps == null) continue;

                var emission = ps.emission;
                var main = ps.main;

                _particleInitialStates[i] = new ParticleInitialState
                {
                    Particle = ps,
                    RateOverTime = emission.rateOverTime,
                    StartSize = main.startSize,
                    StartLifetime = main.startLifetime
                };
            }
        }

        public void TakeExtinguish(float deltaTime)
        {
            if (_isExtinguished || deltaTime <= 0f) return;

            _accumulatedExtinguishTime += deltaTime;

            float duration = Mathf.Max(_extinguishDuration, 0.01f);
            int stageCount = Mathf.Max(_extinguishStageCount, 1);
            float secondsPerStage = duration / stageCount;

            int nextStage = Mathf.FloorToInt(_accumulatedExtinguishTime / secondsPerStage);
            nextStage = Mathf.Clamp(nextStage, 0, stageCount);

            if (nextStage == _currentStage) return;

            _currentStage = nextStage;

            float intensity = 1f - ((float)_currentStage / stageCount);
            ApplyIntensity(intensity);

            if (_currentStage >= stageCount)
            {
                Extinguish();
            }
        }

        private void ApplyIntensity(float intensity)
        {
            foreach (ParticleInitialState state in _particleInitialStates)
            {
                if (state.Particle == null) continue;

                var emission = state.Particle.emission;
                emission.rateOverTime = ScaleCurve(state.RateOverTime, intensity);

                var main = state.Particle.main;
                main.startSize = ScaleCurve(state.StartSize, intensity);
                main.startLifetime = ScaleCurve(state.StartLifetime, intensity);
            }

            if (_audioSource != null)
            {
                _audioSource.volume = _initialVolume * intensity;
            }
        }

        private ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve curve, float scale)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    curve.constant *= scale;
                    break;

                case ParticleSystemCurveMode.TwoConstants:
                    curve.constantMin *= scale;
                    curve.constantMax *= scale;
                    break;

                case ParticleSystemCurveMode.Curve:
                case ParticleSystemCurveMode.TwoCurves:
                    curve.curveMultiplier *= scale;
                    break;
            }

            return curve;
        }

        private void Extinguish()
        {
            _isExtinguished = true;

            OnExtinguished?.Invoke();

            foreach (ParticleSystem ps in _fireParticles)
            {
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (_audioSource != null)
                _audioSource.volume = 0f;

            if (_disableWhenExtinguished)
            {
                gameObject.SetActive(false);
            }
        }

        #region 플레이어 불 진입 (피해 피드백)
        // todo : 나중에 플레이어 UI 관련 요소 생기면 여기서 피드백 연결
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                Debug.Log("=====Player Enter=====");
                // 어기에서 데미지 코루틴
                // other.transform.GetComponent<PlayerUI>().ChangeScreenVignette(true);
                // other.transform.GetComponent<PlayerUI>().PlayDamageSFX(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                Debug.Log("=====Player Exit=====");
                // other.transform.GetComponent<PlayerUI>().ChangeScreenVignette(false);
                // other.transform.GetComponent<PlayerUI>().PlayDamageSFX(false);
            }
        }
        #endregion
    }
}