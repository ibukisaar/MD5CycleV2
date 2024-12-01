#include <cinttypes>
#include <immintrin.h>
#include <memory.h>

#define API_EXPORT extern "C" __declspec(dllexport)

typedef uint32_t u32_4 __attribute__((vector_size(4 * sizeof(uint32_t)), __aligned__(16)));

__attribute__((always_inline))
static __int128 load(const __m128i* p) {
    __m128i r = _mm_lddqu_si128(p);
    return reinterpret_cast<const __int128&>(r);
}

__attribute__((always_inline))
static __int128 bswap(const __int128& x) {
    constexpr __m128i indexes = (__m128i)u32_4{ 0x0c0d0e0f, 0x08090a0b, 0x04050607, 0x00010203 };
    __m128i r = _mm_shuffle_epi8(reinterpret_cast<const __m128i&>(x), indexes);
    return reinterpret_cast<const __int128&>(r);
}


constexpr __int128 mask = ~(__int128)0xffffffffULL;

__attribute__((always_inline))
static __int128 loadbm(const __m128i* a) {
    __int128 x = load(a) & mask;
    return bswap(x);
}



API_EXPORT
int i128_CompareTo(const __m128i* a, const __m128i* b) {
    __int128 x = loadbm(a);
    __int128 y = loadbm(b);
    __int128 r = x - y;
    if (r < 0) return -1;
    if (r > 0) return 1;
    return 0;
}

API_EXPORT
int i128_HashCode(const __m128i* a, int bits) {
    int mask = (1 << bits) - 1;
    __int128 v = load(a);
    return (bswap(v) >> (128 - 32 - bits)) & mask;
}

API_EXPORT
void i128_hash_sub(__int128& __restrict r, const __m128i* a, const __m128i* b) {
    __int128 x = loadbm(a);
    __int128 y = loadbm(b);
    r = x - y;
}

API_EXPORT
void i128_add(__int128& r, const __int128& a) {
    r += a;
}

API_EXPORT
void i128_mins(__int128* __restrict mins, int n, const __int128& other) {
    for (int i = 0; i < n; i++) {
        if (other < mins[i]) {
            memmove(&mins[i + 1], &mins[i], static_cast<size_t>(n - i - 1) * sizeof(__int128));
            mins[i] = other;
            return;
        }
    }
}

API_EXPORT
void i128_maxs(__int128* __restrict maxs, int n, const __int128& other) {
    for (int i = 0; i < n; i++) {
        if (other > maxs[i]) {
            memmove(&maxs[i + 1], &maxs[i], static_cast<size_t>(n - i - 1) * sizeof(__int128));
            maxs[i] = other;
            return;
        }
    }
}