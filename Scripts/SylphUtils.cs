
namespace Sylph {
    public static class SylphUtils {
        public static bool GetBit(int bits, int position) {
            return (bits & (1 << position)) != 0;
        }

        public static int SetBit(int bits, int position) {
            return bits |= (1 << position);
        }
    }
}
