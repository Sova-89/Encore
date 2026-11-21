// ============================================================================
//  Rooms.cs — «эта сцена — комната боя?». Без Unity (проверяется тестами вне игры).
//  В GoP у части боссов объекты лежат не в сцене комнаты, а в её дополнительной сцене: комната Дальних полей — «Bone_East_08»,
//  а Savage Beastfly и Fourth Chorus живут в «Bone_East_08_boss_beastfly» / «Bone_East_08_boss_golem» (по этим именам GoP и
//  привязывает свои правки FSM). Поэтому сцена объекта подходит, если это сама комната или комната + «_что-то».
// ============================================================================
namespace Encore
{
    internal static class Rooms
    {
        /// Сцена actual — это комната room или её дополнительная сцена.
        internal static bool Same(string actual, string room)
        {
            if (actual == null || room == null) return false;
            return actual == room || actual.StartsWith(room + "_");
        }
    }
}
