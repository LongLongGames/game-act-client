using LiteEntitySystem;
using UnityEngine;

namespace GameAct.Les.Shared
{
    public class MonsterBotController : AiControllerLogic<ActMonster>
    {
        float _yaw;
        float _changeTimer;

        public MonsterBotController(EntityParams entityParams) : base(entityParams)
        {
            _yaw = Random.Range(0f, 360f);
            _changeTimer = Random.Range(0.4f, 1.5f);
        }

        protected override void BeforeControlledUpdate()
        {
            var pawn = ControlledEntity;
            if (pawn == null) return;

            _changeTimer -= EntityManager.DeltaTimeF;
            if (_changeTimer <= 0f)
            {
                _yaw += Random.Range(-50f, 50f);
                _changeTimer = Random.Range(0.6f, 2.5f);
            }

            bool idle = Random.Range(0, 40) == 0;
            Vector3 dir = Vector3.zero;
            if (!idle)
            {
                // Unity 约定：yaw=0 朝 +Z，与 ActPlayer 一致
                // forward = (Sin(yaw), 0, Cos(yaw))
                float rad = _yaw * Mathf.Deg2Rad;
                dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            }
            pawn.SetInput(dir, _yaw);
        }
    }
}
