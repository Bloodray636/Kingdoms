using UnityEngine;

namespace KingdomsClone.Gameplay
{
    public class Job
    {
        public JobType Type;
        public JobPriority Priority;
        public BuildingBase Workplace;
        public Vector2Int WorkplaceCell;
        public Vector2Int WorkPosition;  // НОВОЕ: куда именно идти (уникально для каждого job)
        public Villager AssignedVillager;

        public bool IsOccupied => AssignedVillager != null;
        public bool IsFree => AssignedVillager == null;

        public Job(JobType type, JobPriority priority, BuildingBase workplace, Vector2Int cell, Vector2Int workPosition)
        {
            Type = type;
            Priority = priority;
            Workplace = workplace;
            WorkplaceCell = cell;
            WorkPosition = workPosition;
        }

        public override string ToString()
        {
            string assigned = AssignedVillager != null ? AssignedVillager.gameObject.name : "—";
            return $"{Type} [{Priority}] at {WorkplaceCell} (pos {WorkPosition}) → {assigned}";
        }
    }
}