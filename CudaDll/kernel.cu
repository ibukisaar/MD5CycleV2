
#include "cuda_runtime.h"
#include "device_launch_parameters.h"

#include <cinttypes>
#include <algorithm>

#define API_EXPORT extern "C" __declspec(dllexport) __host__

template<typename T> constexpr T Mask = 0x00'00'00'ff;

struct alignas(16) hash_t {
    union {
        struct { uint32_t a, b, c, d; };
        uint32_t hash[4];
        struct { uint64_t l0, l1; };
    };

    __device__ __host__ hash_t() = default;
    __device__ __host__ hash_t(uint32_t a, uint32_t b, uint32_t c, uint32_t d) : a(a), b(b), c(c), d(d) {}

    //__device__ __host__ hash_t() : a(0), b(0), c(0), d(0) {}
    //__device__ __host__ hash_t(uint32_t v) : a(v), b(0), c(0), d(0) {}

    __device__ __host__ bool operator==(const hash_t& other) const {
        return l0 == other.l0 && l1 == other.l1;
    }

    __device__ __host__ bool operator!=(const hash_t& other) const {
        return l0 != other.l0 || l1 != other.l1;
    }

    __device__ __host__ hash_t mask() const {
        hash_t r;
        r.a = a;
        r.b = b;
        r.c = c;
        r.d = d & Mask<uint32_t>;
        return r;
    }
};

template<size_t N>
struct alignas(4 * N) vec_t {
    uint32_t v[N];

    __device__ __host__ constexpr vec_t() = default;

    __device__ __host__ constexpr vec_t(uint32_t v) : v{} {
#pragma unroll
        for (size_t i = 0; i < N; i++) {
            this->v[i] = v;
        }
    }

    __device__ __host__ constexpr uint32_t& operator[](size_t i) {
        return v[i];
    }

    __device__ __host__ constexpr uint32_t operator[](size_t i) const {
        return v[i];
    }

    __device__ __host__ constexpr vec_t operator+(const vec_t& other) const {
        vec_t r;
#pragma unroll
        for (size_t i = 0; i < N; i++) r[i] = v[i] + other[i];
        return r;
    }

    __device__ __host__ constexpr vec_t operator&(const vec_t& other) const {
        vec_t r;
#pragma unroll
        for (size_t i = 0; i < N; i++) r[i] = v[i] & other[i];
        return r;
    }

    __device__ __host__ constexpr vec_t operator^(const vec_t& other) const {
        vec_t r;
#pragma unroll
        for (size_t i = 0; i < N; i++) r[i] = v[i] ^ other[i];
        return r;
    }

    __device__ __host__ constexpr vec_t operator|(const vec_t& other) const {
        vec_t r;
#pragma unroll
        for (size_t i = 0; i < N; i++) r[i] = v[i] | other[i];
        return r;
    }

    __device__ __host__ constexpr vec_t operator~() const {
        vec_t r;
#pragma unroll
        for (size_t i = 0; i < N; i++) r[i] = ~v[i];
        return r;
    }

    __device__ __host__ constexpr vec_t RL(int shift) const {
        vec_t r;
#pragma unroll
        for (size_t i = 0; i < N; i++) {
            r[i] = (v[i] << shift) | (v[i] >> (32 - shift));
        }
        return r;
    }
};

constexpr size_t V = 2;

struct alignas(64) hash_vec_t {
    using vec_t = ::vec_t<V>;

    union {
        struct { vec_t a, b, c, d; };
        vec_t hash[4];
    };

    __device__ __host__ hash_t operator[](size_t i) const {
        hash_t h;
        h.a = a[i];
        h.b = b[i];
        h.c = c[i];
        h.d = d[i];
        return h;
    }
};

template<size_t N> __device__ __forceinline static vec_t<N> RL(const vec_t<N>& x, int n) { return x.RL(n); }

template<typename T> __device__ __forceinline static T F(T x, T y, T z) { return (x & (y ^ z)) ^ z; }
template<typename T> __device__ __forceinline static T G(T x, T y, T z) { return (z & (x ^ y)) ^ y; }
template<typename T> __device__ __forceinline static T H(T x, T y, T z) { return x ^ y ^ z; }
template<typename T> __device__ __forceinline static T I(T x, T y, T z) { return y ^ (x | ~z); }
template<typename T> __device__ __forceinline static T RL(T x, int n) { return (x << n) | (x >> (32 - n)); }

#define R(f, a, b, c, d, m, k, s) \
    a = a + (f(b, c, d) + m + k); \
    a = RL(a, s) + b;



template<typename T, bool UseMask = false>
__device__ static void md5(T hash[4]) {
    T M[16]{};
    M[0] = hash[0];
    M[1] = hash[1];
    M[2] = hash[2];
    M[3] = UseMask ? (hash[3] & Mask<T>) : hash[3];
    M[4] = 0x80;
    M[14] = 128;

    constexpr T A = 0x67452301u, B = 0xefcdab89u, C = 0x98badcfeu, D = 0x10325476u;

    T a = A;
    T b = B;
    T c = C;
    T d = D;

    R(F, a, b, c, d, M[0], 0xd76aa478, 7);
    R(F, d, a, b, c, M[1], 0xe8c7b756, 12);
    R(F, c, d, a, b, M[2], 0x242070db, 17);
    R(F, b, c, d, a, M[3], 0xc1bdceee, 22);
    R(F, a, b, c, d, M[4], 0xf57c0faf, 7);
    R(F, d, a, b, c, M[5], 0x4787c62a, 12);
    R(F, c, d, a, b, M[6], 0xa8304613, 17);
    R(F, b, c, d, a, M[7], 0xfd469501, 22);
    R(F, a, b, c, d, M[8], 0x698098d8, 7);
    R(F, d, a, b, c, M[9], 0x8b44f7af, 12);
    R(F, c, d, a, b, M[10], 0xffff5bb1, 17);
    R(F, b, c, d, a, M[11], 0x895cd7be, 22);
    R(F, a, b, c, d, M[12], 0x6b901122, 7);
    R(F, d, a, b, c, M[13], 0xfd987193, 12);
    R(F, c, d, a, b, M[14], 0xa679438e, 17);
    R(F, b, c, d, a, M[15], 0x49b40821, 22);

    R(G, a, b, c, d, M[1], 0xf61e2562, 5);
    R(G, d, a, b, c, M[6], 0xc040b340, 9);
    R(G, c, d, a, b, M[11], 0x265e5a51, 14);
    R(G, b, c, d, a, M[0], 0xe9b6c7aa, 20);
    R(G, a, b, c, d, M[5], 0xd62f105d, 5);
    R(G, d, a, b, c, M[10], 0x02441453, 9);
    R(G, c, d, a, b, M[15], 0xd8a1e681, 14);
    R(G, b, c, d, a, M[4], 0xe7d3fbc8, 20);
    R(G, a, b, c, d, M[9], 0x21e1cde6, 5);
    R(G, d, a, b, c, M[14], 0xc33707d6, 9);
    R(G, c, d, a, b, M[3], 0xf4d50d87, 14);
    R(G, b, c, d, a, M[8], 0x455a14ed, 20);
    R(G, a, b, c, d, M[13], 0xa9e3e905, 5);
    R(G, d, a, b, c, M[2], 0xfcefa3f8, 9);
    R(G, c, d, a, b, M[7], 0x676f02d9, 14);
    R(G, b, c, d, a, M[12], 0x8d2a4c8a, 20);

    R(H, a, b, c, d, M[5], 0xfffa3942, 4);
    R(H, d, a, b, c, M[8], 0x8771f681, 11);
    R(H, c, d, a, b, M[11], 0x6d9d6122, 16);
    R(H, b, c, d, a, M[14], 0xfde5380c, 23);
    R(H, a, b, c, d, M[1], 0xa4beea44, 4);
    R(H, d, a, b, c, M[4], 0x4bdecfa9, 11);
    R(H, c, d, a, b, M[7], 0xf6bb4b60, 16);
    R(H, b, c, d, a, M[10], 0xbebfbc70, 23);
    R(H, a, b, c, d, M[13], 0x289b7ec6, 4);
    R(H, d, a, b, c, M[0], 0xeaa127fa, 11);
    R(H, c, d, a, b, M[3], 0xd4ef3085, 16);
    R(H, b, c, d, a, M[6], 0x04881d05, 23);
    R(H, a, b, c, d, M[9], 0xd9d4d039, 4);
    R(H, d, a, b, c, M[12], 0xe6db99e5, 11);
    R(H, c, d, a, b, M[15], 0x1fa27cf8, 16);
    R(H, b, c, d, a, M[2], 0xc4ac5665, 23);

    R(I, a, b, c, d, M[0], 0xf4292244, 6);
    R(I, d, a, b, c, M[7], 0x432aff97, 10);
    R(I, c, d, a, b, M[14], 0xab9423a7, 15);
    R(I, b, c, d, a, M[5], 0xfc93a039, 21);
    R(I, a, b, c, d, M[12], 0x655b59c3, 6);
    R(I, d, a, b, c, M[3], 0x8f0ccc92, 10);
    R(I, c, d, a, b, M[10], 0xffeff47d, 15);
    R(I, b, c, d, a, M[1], 0x85845dd1, 21);
    R(I, a, b, c, d, M[8], 0x6fa87e4f, 6);
    R(I, d, a, b, c, M[15], 0xfe2ce6e0, 10);
    R(I, c, d, a, b, M[6], 0xa3014314, 15);
    R(I, b, c, d, a, M[13], 0x4e0811a1, 21);
    R(I, a, b, c, d, M[4], 0xf7537e82, 6);
    R(I, d, a, b, c, M[11], 0xbd3af235, 10);
    R(I, c, d, a, b, M[2], 0x2ad7d2bb, 15);
    R(I, b, c, d, a, M[9], 0xeb86d391, 21);

    hash[0] = a + A;
    hash[1] = b + B;
    hash[2] = c + C;
    hash[3] = d + D;
}

__device__ static uint8_t hex2char(uint8_t x) {
    //constexpr uint64_t L = 0x37'36'35'34'33'32'31'30;
    //constexpr uint64_t H = 0x66'65'64'63'62'61'39'38;
    return x > 9 ? 'a' + x - 10 : '0' + x;
    //uint8_t a = x / 10;
    //uint8_t b = x % 10;
    //return a ? b + 97 : b + 48;
}

__device__ static uint16_t hex2str(uint8_t x) {
    uint8_t r[2];
    r[0] = hex2char(x >> 4);
    r[1] = hex2char(x & 15);
    return *reinterpret_cast<uint16_t*>(r);
}

__device__ static uint32_t hex2str(uint16_t x, const uint16_t* cache) {
    uint8_t r[sizeof(uint32_t)];
    for (int i = 0; i < 2; i++) {
        uint8_t b = x >> (i * 8);
        reinterpret_cast<uint16_t*>(r)[i] = cache[b];
    }
    return *reinterpret_cast<uint32_t*>(r);
}

__device__ static uint64_t hex2str(uint32_t x, const uint16_t* cache) {
    uint8_t r[sizeof(uint64_t)];
    for (int i = 0; i < 4; i++) {
        uint8_t b = x >> (i * 8);
        reinterpret_cast<uint16_t*>(r)[i] = cache[b];
    }
    return *reinterpret_cast<uint64_t*>(r);
}

__device__ __forceinline static uint64_t md5_114514(const uint64_t* prefix, uint64_t hash_d) {
    uint32_t M[16]{};
    //M[0] = '5411';
    //M[1] = '9141';
    //M[2] = '1891';
    //M[3] = '0' | (hex2str((uint16_t)hash[0]) << 8); // 3
    //*reinterpret_cast<uint64_t*>(&M[4]) = hex2str(hash[2]); // 8
    //*reinterpret_cast<uint64_t*>(&M[6]) = hex2str(hash[3]); // 8
    memcpy(M + 0, prefix, 24);
    memcpy(M + 6, &hash_d, sizeof(uint64_t));
    //*reinterpret_cast<uint64_t*>(&M[6]) = hash_d;
    M[8] = 0x80;
    M[14] = 256;

    constexpr uint32_t A = 0x67452301u, B = 0xefcdab89u, C = 0x98badcfeu, D = 0x10325476u;

    uint32_t a = A;
    uint32_t b = B;
    uint32_t c = C;
    uint32_t d = D;

    R(F, a, b, c, d, M[0], 0xd76aa478, 7);
    R(F, d, a, b, c, M[1], 0xe8c7b756, 12);
    R(F, c, d, a, b, M[2], 0x242070db, 17);
    R(F, b, c, d, a, M[3], 0xc1bdceee, 22);
    R(F, a, b, c, d, M[4], 0xf57c0faf, 7);
    R(F, d, a, b, c, M[5], 0x4787c62a, 12);
    R(F, c, d, a, b, M[6], 0xa8304613, 17);
    R(F, b, c, d, a, M[7], 0xfd469501, 22);
    R(F, a, b, c, d, M[8], 0x698098d8, 7);
    R(F, d, a, b, c, M[9], 0x8b44f7af, 12);
    R(F, c, d, a, b, M[10], 0xffff5bb1, 17);
    R(F, b, c, d, a, M[11], 0x895cd7be, 22);
    R(F, a, b, c, d, M[12], 0x6b901122, 7);
    R(F, d, a, b, c, M[13], 0xfd987193, 12);
    R(F, c, d, a, b, M[14], 0xa679438e, 17);
    R(F, b, c, d, a, M[15], 0x49b40821, 22);

    R(G, a, b, c, d, M[1], 0xf61e2562, 5);
    R(G, d, a, b, c, M[6], 0xc040b340, 9);
    R(G, c, d, a, b, M[11], 0x265e5a51, 14);
    R(G, b, c, d, a, M[0], 0xe9b6c7aa, 20);
    R(G, a, b, c, d, M[5], 0xd62f105d, 5);
    R(G, d, a, b, c, M[10], 0x02441453, 9);
    R(G, c, d, a, b, M[15], 0xd8a1e681, 14);
    R(G, b, c, d, a, M[4], 0xe7d3fbc8, 20);
    R(G, a, b, c, d, M[9], 0x21e1cde6, 5);
    R(G, d, a, b, c, M[14], 0xc33707d6, 9);
    R(G, c, d, a, b, M[3], 0xf4d50d87, 14);
    R(G, b, c, d, a, M[8], 0x455a14ed, 20);
    R(G, a, b, c, d, M[13], 0xa9e3e905, 5);
    R(G, d, a, b, c, M[2], 0xfcefa3f8, 9);
    R(G, c, d, a, b, M[7], 0x676f02d9, 14);
    R(G, b, c, d, a, M[12], 0x8d2a4c8a, 20);

    R(H, a, b, c, d, M[5], 0xfffa3942, 4);
    R(H, d, a, b, c, M[8], 0x8771f681, 11);
    R(H, c, d, a, b, M[11], 0x6d9d6122, 16);
    R(H, b, c, d, a, M[14], 0xfde5380c, 23);
    R(H, a, b, c, d, M[1], 0xa4beea44, 4);
    R(H, d, a, b, c, M[4], 0x4bdecfa9, 11);
    R(H, c, d, a, b, M[7], 0xf6bb4b60, 16);
    R(H, b, c, d, a, M[10], 0xbebfbc70, 23);
    R(H, a, b, c, d, M[13], 0x289b7ec6, 4);
    R(H, d, a, b, c, M[0], 0xeaa127fa, 11);
    R(H, c, d, a, b, M[3], 0xd4ef3085, 16);
    R(H, b, c, d, a, M[6], 0x04881d05, 23);
    R(H, a, b, c, d, M[9], 0xd9d4d039, 4);
    R(H, d, a, b, c, M[12], 0xe6db99e5, 11);
    R(H, c, d, a, b, M[15], 0x1fa27cf8, 16);
    R(H, b, c, d, a, M[2], 0xc4ac5665, 23);

    R(I, a, b, c, d, M[0], 0xf4292244, 6);
    R(I, d, a, b, c, M[7], 0x432aff97, 10);
    R(I, c, d, a, b, M[14], 0xab9423a7, 15);
    R(I, b, c, d, a, M[5], 0xfc93a039, 21);
    R(I, a, b, c, d, M[12], 0x655b59c3, 6);
    R(I, d, a, b, c, M[3], 0x8f0ccc92, 10);
    R(I, c, d, a, b, M[10], 0xffeff47d, 15);
    R(I, b, c, d, a, M[1], 0x85845dd1, 21);
    R(I, a, b, c, d, M[8], 0x6fa87e4f, 6);
    R(I, d, a, b, c, M[15], 0xfe2ce6e0, 10);
    R(I, c, d, a, b, M[6], 0xa3014314, 15);
    R(I, b, c, d, a, M[13], 0x4e0811a1, 21);
    R(I, a, b, c, d, M[4], 0xf7537e82, 6);
    R(I, d, a, b, c, M[11], 0xbd3af235, 10);
    R(I, c, d, a, b, M[2], 0x2ad7d2bb, 15);
    R(I, b, c, d, a, M[9], 0xeb86d391, 21);

    //hash_t r;
    //r.a = a + A;
    //r.b = b + B;
    //r.c = c + C;
    //r.d = d + D;
    //return r;

    //const uint32_t r[2]{ a + A, b + B };
    //return *reinterpret_cast<const uint64_t*>(r);
    return static_cast<uint64_t>(a + A) | (static_cast<uint64_t>(b + B) << 32);
}

struct result_t {
    union {
        hash_t __align__(4) hash;
        uint32_t index;
    };
    uint64_t iterateCnt;
};

struct result114514_t {
    hash_t __align__(4) hash;
    uint32_t index;
    uint32_t iterateCnt;
};


static int blockCount, threadCount;
static hash_t* cpu_hashes;
static result_t* cpu_result;
static result114514_t* cpu_result114514;
static int cpu_maxResultCount;

__constant__ static hash_t* hashes;
__constant__ static result_t* result;
__constant__ static result114514_t* result114514;
__constant__ static int maxResultCount;
__device__ static int resultCount;

constexpr uint64_t N = 1 << 24;

__global__ void gpu_md5(uint64_t start) {
    uint32_t id = blockDim.x * blockIdx.x + threadIdx.x;
    hash_t h = hashes[id];

#pragma unroll 2
    for (uint64_t n = 0; n < N; n++) {
        md5<uint32_t>(h.hash);

        if (h.a == 0) { // 32 bits
            int resultIndex = atomicAdd(&resultCount, 1);
            if (resultIndex < maxResultCount) {
                result[resultIndex].hash = h;
                result[resultIndex].index = id;
                result[resultIndex].iterateCnt = start + n + 1;
            }
        }

        //__syncthreads();
    }

    hashes[id] = h;
}

__global__ void gpu_md5_mask(uint64_t start) {
    uint32_t id = blockDim.x * blockIdx.x + threadIdx.x;
    hash_t h = hashes[id];

#pragma unroll 2
    for (uint64_t n = 0; n < N; n++) {
        md5<uint32_t, true>(h.hash);

        if (h.a == 0) { // 32 bits
            int resultIndex = atomicAdd(&resultCount, 1);
            if (resultIndex < maxResultCount) {
                result[resultIndex].hash = h.mask();
                result[resultIndex].index = id;
                result[resultIndex].iterateCnt = start + n + 1;
            }
        }

        //__syncthreads();
    }

    hashes[id] = h;
}

__global__ void gpu_md5_vec(uint64_t start) {
    uint32_t idStart = (blockDim.x * blockIdx.x + threadIdx.x) * V;
    hash_vec_t h;

    for (size_t i = 0; i < V; i++) {
        h.a[i] = hashes[idStart + i].a;
        h.b[i] = hashes[idStart + i].b;
        h.c[i] = hashes[idStart + i].c;
        h.d[i] = hashes[idStart + i].d;
    }

#pragma unroll 2
    for (uint64_t n = 0; n < N; n++) {
        md5<hash_vec_t::vec_t>(h.hash);

#pragma unroll
        for (size_t i = 0; i < V; i++) {
            if (h.a[i] == 0) {
                int resultIndex = atomicAdd(&resultCount, 1);
                if (resultIndex < maxResultCount) {
                    result[resultIndex].hash = h[i];
                    result[resultIndex].index = idStart + i;
                    result[resultIndex].iterateCnt = start + n + 1;
                }
            }
        }

        //__syncthreads();
    }

    for (size_t i = 0; i < V; i++) {
        hashes[idStart + i].a = h.a[i];
        hashes[idStart + i].b = h.b[i];
        hashes[idStart + i].c = h.c[i];
        hashes[idStart + i].d = h.d[i];
    }
}

API_EXPORT
int md5(uint64_t& start, result_t* result, int useMask) {
    int resultCount = 0;
    cudaMemcpyToSymbol(::resultCount, &resultCount, sizeof(int));

    if (useMask) {
        gpu_md5_mask << <blockCount, threadCount >> > (start);
    }
    else {
        gpu_md5 << <blockCount, threadCount >> > (start);
    }

    cudaError_t error = cudaGetLastError();
    if (error != cudaSuccess) {
        return -(int)error;
    }
    cudaDeviceSynchronize();

    cudaMemcpyFromSymbol(&resultCount, ::resultCount, sizeof(int));

    if (resultCount) {
        int copyCount = std::min(resultCount, cpu_maxResultCount);
        cudaMemcpy(result, cpu_result, (size_t)copyCount * sizeof(result_t), cudaMemcpyDeviceToHost);
    }

    start += N;
    return resultCount;
}

API_EXPORT
int md5_vec(uint64_t& start, result_t* result) {
    int resultCount = 0;
    cudaMemcpyToSymbol(::resultCount, &resultCount, sizeof(int));

    gpu_md5_vec << <blockCount, (threadCount / V) >> > (start);
    cudaError_t error = cudaGetLastError();
    if (error != cudaSuccess) {
        return -(int)error;
    }
    cudaDeviceSynchronize();

    cudaMemcpyFromSymbol(&resultCount, ::resultCount, sizeof(int));

    if (resultCount) {
        int copyCount = std::min(resultCount, cpu_maxResultCount);
        cudaMemcpy(result, cpu_result, (size_t)copyCount * sizeof(result_t), cudaMemcpyDeviceToHost);
    }

    start += N;
    return resultCount;
}

__global__ void gpu_114514_md5(uint64_t start) {
    uint32_t id = blockIdx.x;
    
    __shared__ uint16_t hex_cache[256];
    __shared__ uint64_t common_prefix[3];

    if (threadIdx.x < 32) {
        for (int i = 0; i < 256; i += 32) {
            hex_cache[i + threadIdx.x] = hex2str((uint8_t)(i + threadIdx.x));
        }
    }

    __syncthreads();

    if (threadIdx.x == 0) {
        hash_t h = hashes[id];
        common_prefix[0] = 0x39'31'34'31'35'34'31'31;
        common_prefix[1] = 0x30'31'38'39'31 | ((uint64_t)hex2str((uint16_t)h.a, hex_cache) << 40);
        common_prefix[2] = hex2str(h.c, hex_cache);
    }

    __syncthreads();

    uint64_t prefix3 = (uint64_t)hex_cache[threadIdx.x] << 48;

    for (uint32_t i2 = 0; i2 < 256; i2++) {
        uint64_t prefix2 = prefix3 | ((uint64_t)hex_cache[i2] << 32);
        for (uint32_t i1 = 0; i1 < 256; i1++) {
            uint64_t prefix1 = prefix2 | ((uint64_t)hex_cache[i1] << 16);
            for (uint32_t i0 = 0; i0 < 256; i0++) {
                uint64_t prefix0 = prefix1 | (uint64_t)hex_cache[i0];
                uint64_t r = md5_114514(common_prefix, prefix0);

                // 0x00811919'144511
                if ((r & 0x00ffffff'ffffff) == 0x00811919'144511) {
                    int resultIndex = atomicAdd(&resultCount, 1);
                    if (resultIndex < maxResultCount) {
                        hash_t h = hashes[id];
                        result114514[resultIndex].hash = hash_t(h.a, h.b, h.c, threadIdx.x * 256 * 256 * 256 + i2 * 256 * 256 + i1 * 256 + i0);
                        //result114514[resultIndex].index = id;
                        //result114514[resultIndex].iterateCnt = start + n + 1;
                    }
                }
            }
        }
    }
}

API_EXPORT
int _114514_md5(uint64_t& start, result114514_t* result) {
    int resultCount = 0;
    cudaMemcpyToSymbol(::resultCount, &resultCount, sizeof(int));

    gpu_114514_md5 << <blockCount, threadCount >> > (start);

    cudaError_t error = cudaGetLastError();
    if (error != cudaSuccess) {
        return -(int)error;
    }
    cudaDeviceSynchronize();

    cudaMemcpyFromSymbol(&resultCount, ::resultCount, sizeof(int));

    if (resultCount) {
        int copyCount = std::min(resultCount, cpu_maxResultCount);
        cudaMemcpy(result, cpu_result114514, (size_t)copyCount * sizeof(result114514_t), cudaMemcpyDeviceToHost);
    }

    start += N;
    return resultCount;
}

API_EXPORT
void read_hashes(hash_t* output) {
    cudaMemcpy(output, cpu_hashes, (size_t)blockCount * threadCount * sizeof(hash_t), cudaMemcpyDeviceToHost);
}

API_EXPORT
const char* get_error(int error) {
    return cudaGetErrorString((cudaError_t)-error);
}

API_EXPORT
void init(int blockCount, int threadCount, int maxResultCount, const hash_t* input) {
    cudaMalloc(&cpu_hashes, (size_t)blockCount * threadCount * sizeof(hash_t));
    if (input) {
        cudaMemcpy(cpu_hashes, input, (size_t)blockCount * threadCount * sizeof(hash_t), cudaMemcpyHostToDevice);
    }
    cudaMemcpyToSymbol(hashes, &cpu_hashes, sizeof(void*));

    cudaMalloc(&cpu_result, (size_t)maxResultCount * sizeof(result_t));
    cudaMemcpyToSymbol(result, &cpu_result, sizeof(void*));

    cudaMemcpyToSymbol(::maxResultCount, &maxResultCount, sizeof(int));

    cudaSetDeviceFlags(cudaDeviceBlockingSync);

    ::blockCount = blockCount;
    ::threadCount = threadCount;
    cpu_maxResultCount = maxResultCount;
}

API_EXPORT
void init114514(int blockCount, int threadCount, int maxResultCount) {
    cudaMalloc(&cpu_hashes, (size_t)blockCount * sizeof(hash_t));
    cudaMemcpyToSymbol(hashes, &cpu_hashes, sizeof(void*));

    cudaMalloc(&cpu_result114514, (size_t)maxResultCount * sizeof(result114514_t));
    cudaMemcpyToSymbol(result114514, &cpu_result114514, sizeof(void*));

    cudaMemcpyToSymbol(::maxResultCount, &maxResultCount, sizeof(int));

    cudaSetDeviceFlags(cudaDeviceBlockingSync);

    ::blockCount = blockCount;
    ::threadCount = threadCount;
    cpu_maxResultCount = maxResultCount;
}

API_EXPORT
void write_hashes(const hash_t* input) {
    cudaMemcpy(cpu_hashes, input, (size_t)blockCount * sizeof(hash_t), cudaMemcpyHostToDevice);
}

API_EXPORT
void release() {
    cudaFree(cpu_hashes);
    cudaFree(cpu_result);
}

API_EXPORT
void release114514() {
    cudaFree(cpu_hashes);
    cudaFree(cpu_result114514);
}