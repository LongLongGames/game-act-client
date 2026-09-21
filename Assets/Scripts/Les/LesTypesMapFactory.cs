using LiteEntitySystem;
using UnityEngine;
using GameAct.Les.Shared;

namespace GameAct.Les
{
    public static class LesTypesMapFactory
    {
        public const int TickRate = 30;
        public const byte HeaderByte = (byte)LesPacketType.EntitySystem;

        static bool _fieldsRegistered;

        public static void EnsureFieldTypes()
        {
            if (LiteEntitySystem.Logger.LoggerImpl == null)
                LiteEntitySystem.Logger.LoggerImpl = new UnityLesLogger();

            if (_fieldsRegistered) return;
            LiteEntitySystem.EntityManager.RegisterFieldType<Vector3>(Vector3.Lerp);
            _fieldsRegistered = true;
        }

        public static EntityTypesMap<GameEntities> Create()
        {
            EnsureFieldTypes();
            return new EntityTypesMap<GameEntities>()
                .Register(GameEntities.Enemy, e => new ActEnemy(e))
                .Register(GameEntities.EnemyBot, e => new EnemyBotController(e))
                .Register(GameEntities.Player, e => new ActPlayer(e))
                .Register(GameEntities.PlayerController, e => new ActPlayerController(e));
        }

        public static ulong EvaluateHash() => Create().EvaluateEntityClassDataHash();
    }
}
