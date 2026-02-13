namespace ProjectA.Networking
{
    public class NetworkMOST_Damage
    {
        public static void Apply(NetworkCharacter target, int damage, int attackerPlayerRaw)
        {
            if (target == null) return;
            target.ApplyDamage(damage, attackerPlayerRaw);
        }
    }
}
