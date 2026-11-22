using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;
using KingdomsClone.Grid;

namespace KingdomsClone.Gameplay
{
    [DefaultExecutionOrder(-720)]
    public class JobManager : MonoBehaviour
    {
        public static JobManager Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Как часто проверять безработных и свободные вакансии (сек).")]
        [SerializeField] private float assignmentTickSeconds = 2f;

        [Tooltip("Радиус, в котором ищем работу. За пределами — не назначаем.")]
        [SerializeField] private float maxAssignmentRadius = 100f;

        private readonly List<Villager> _unemployed = new();
        private readonly List<Job> _vacancies = new();
        private float _tickTimer;

        public IReadOnlyList<Villager> Unemployed => _unemployed;
        public IReadOnlyList<Job> Vacancies => _vacancies;
        public int TotalJobs => _vacancies.Count;
        public int OccupiedJobs => _vacancies.FindAll(j => j.IsOccupied).Count;

        private void Awake()
        {
            if (Instance != null && Instance != this) { 
                Destroy(gameObject); 
                return; 
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { 
            if (Instance == this) {
                Instance = null;
            } 
        }

        private void OnEnable()
        {
            EventBus.Subscribe<BuildingRemovedEvent>(OnBuildingRemoved);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BuildingRemovedEvent>(OnBuildingRemoved);
        }

        private void Update()
        {
            _tickTimer += Time.deltaTime;

            if (_tickTimer < assignmentTickSeconds) return;

            _tickTimer = 0f;
            TryAssignAll();
        }

        // РЕГИСТРАЦИЯ ВАКАНСИЙ

        public void RegisterVacancy(Job job)
        {
            if (job == null) return;
            if (_vacancies.Contains(job)) return;

            _vacancies.Add(job);
        }

        public void UnregisterVacancy(Job job)
        {
            if (job == null) return;
            _vacancies.Remove(job);

            if (job.AssignedVillager != null) {
                var v = job.AssignedVillager;
                job.AssignedVillager = null;

                if (v != null) {
                    v.ClearJob();
                }
            }
        }

        private void OnBuildingRemoved(BuildingRemovedEvent e)
        {
            var toRemove = new List<Job>();

            foreach (var job in _vacancies) {
                if (job.Workplace == null) { 
                    toRemove.Add(job); 
                    continue; 
                }

                if (job.Workplace.Data == null) { 
                    toRemove.Add(job); 
                    continue; 
                }

                if (job.Workplace.State != BuildingState.Active) { 
                    toRemove.Add(job); 
                    continue; 
                }
            }

            foreach (var job in toRemove) {
                var v = job.AssignedVillager;
                job.AssignedVillager = null;
                _vacancies.Remove(job);

                if (v != null) {
                    v.ClearJob();
                    RegisterUnemployed(v);
                }
            }

            if (toRemove.Count > 0) {
                Debug.Log($"[JobManager] Снято {toRemove.Count} вакансий неактивных зданий");
            }

            TryAssignAll();
        }

        // РЕГИСТРАЦИЯ ЖИТЕЛЕЙ

        public void RegisterUnemployed(Villager v)
        {
            if (v == null) return;

            if (!_unemployed.Contains(v)) {
                _unemployed.Add(v);
            }
        }

        public void UnregisterUnemployed(Villager v)
        {
            if (v != null) {
                _unemployed.Remove(v);
            }
        }

        // НАЗНАЧЕНИЕ

        public void TryAssignAll()
        {
            if (_unemployed.Count == 0 || _vacancies.Count == 0) return;

            var vm = VillagerManager.Instance;
            if (vm == null) return;

            // Проходим по всем безработным и пытаемся найти им вакансию
            var toRemove = new List<Villager>();

            foreach (var v in _unemployed)
            {
                if (v == null) { 
                    toRemove.Add(v); 
                    continue; 
                }

                if (v.CurrentJob != null) { 
                    toRemove.Add(v); 
                    continue; 
                }

                var bestJob = FindBestJobFor(v);
                if (bestJob == null) continue;

                Assign(v, bestJob);
                toRemove.Add(v);
            }

            foreach (var v in toRemove) {
                _unemployed.Remove(v);
            }
        }

        private Job FindBestJobFor(Villager v)
        {
            var grid = GridSystem.Instance;
            if (grid == null) return null;

            Vector2Int vCell = grid.WorldToCell(v.transform.position);
            Job best = null;
            int bestPriority = -1;
            float bestDist = float.MaxValue;

            foreach (var job in _vacancies)
            {
                if (!job.IsFree) continue;
                if (job.Workplace == null || job.Workplace.Data == null) continue;

                int p = (int)job.Priority;
                float dist = Vector2Int.Distance(vCell, job.WorkplaceCell);

                if (dist > maxAssignmentRadius) continue;

                if (p > bestPriority || (p == bestPriority && dist < bestDist)) {
                    best = job;
                    bestPriority = p;
                    bestDist = dist;
                }
            }
            return best;
        }

        private void Assign(Villager v, Job job)
        {
            job.AssignedVillager = v;
            v.AssignJob(job);

            Debug.Log($"[JobManager] {v.gameObject.name} → {job.Type} [{job.Priority}] " + $"в {job.WorkplaceCell} (dist={Vector2Int.Distance(GridSystem.Instance.WorldToCell(v.transform.position), job.WorkplaceCell):F1})");
        }

        // API

        public void ReleaseJob(Job job)
        {
            if (job == null) return;

            var v = job.AssignedVillager;
            job.AssignedVillager = null;

            if (v != null)  {
                v.ClearJob();
                RegisterUnemployed(v);
            }
        }

        public List<Job> GetVacanciesAt(BuildingBase building)
        {
            var result = new List<Job>();

            foreach (var j in _vacancies) {
                if (j.Workplace == building) {
                    result.Add(j);
                }
            }

            return result;
        }

        public void ClearAllUnemployed()
        {
            _unemployed.Clear();
        }
    }
}