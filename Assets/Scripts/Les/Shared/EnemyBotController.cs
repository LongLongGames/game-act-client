using LiteEntitySystem;
using UnityEngine;

namespace GameAct.Les.Shared
{
    /// <summary>
    /// 参考 LES 样例 ServerBotController：AiControllerLogic 驱动 ActEnemy 漫游。
    /// 仅在 ServerEntityManager（Solo/Host 权威）上创建。
    /// </summary>
    public class EnemyBotController : AiControllerLogic<ActEnemy>
    {
        float _yaw;
        float _changeTimer;

        public EnemyBotController(EntityParams entityParams) : base(entityParams)
        {
            _yaw = Random.Range(0f, 360f);
            _changeTimer = Random.Range(0.4f, 1.5f);
        }

        protected override void BeforeControlledUpdate()
        {
            var pawn = ControlledEntity;
            if (pawn == null)
                return;

            float dt = EntityManager.DeltaTimeF;
            _changeTimer -= dt;
            if (_changeTimer <= 0f)
            {
                _yaw += Random.Range(-50f, 50f);
                _changeTimer = Random.Range(0.6f, 2.5f);
            }

            // 偶尔停一下，避免整图敌人同频转圈
            bool idle = Random.Range(0, 40) == 0;
            Vector3 dir = Vector3.zero;
            if (!idle)
            {
                float rad = _yaw * Mathf.Deg2Rad;
                dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            }

            pawn.SetInput(dir, _yaw);
        }
    }
}
