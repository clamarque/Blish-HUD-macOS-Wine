// Test de transparence par pixel sous Wine (macOS) via UpdateLayeredWindow.
// Affiche pendant 60 s : fond totalement transparent, carré rouge semi-transparent (alpha 50 %),
// cadre vert opaque et bande dégradée d'alpha. Écrit le résultat des appels dans layered_test.log.
#include <windows.h>
#include <stdio.h>

#define W 600
#define H 400

static FILE *logf;

static LRESULT CALLBACK WndProc(HWND h, UINT m, WPARAM w, LPARAM l) {
    if (m == WM_DESTROY) { PostQuitMessage(0); return 0; }
    return DefWindowProcW(h, m, w, l);
}

static void put(DWORD *px, int x, int y, BYTE r, BYTE g, BYTE b, BYTE a) {
    // Format BGRA prémultiplié attendu par UpdateLayeredWindow (AC_SRC_ALPHA)
    px[y * W + x] = ((DWORD)a << 24) | ((DWORD)(r * a / 255) << 16) | ((DWORD)(g * a / 255) << 8) | (DWORD)(b * a / 255);
}

int WINAPI WinMain(HINSTANCE hi, HINSTANCE hp, LPSTR cmd, int show) {
    logf = fopen("layered_test.log", "w");

    WNDCLASSW wc = {0};
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hi;
    wc.lpszClassName = L"LayeredTest";
    RegisterClassW(&wc);

    HWND hwnd = CreateWindowExW(WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW,
                                L"LayeredTest", L"LayeredTest", WS_POPUP,
                                200, 200, W, H, NULL, NULL, hi, NULL);
    fprintf(logf, "CreateWindowEx: %p (err %lu)\n", (void *)hwnd, GetLastError());

    BITMAPINFO bi = {0};
    bi.bmiHeader.biSize = sizeof(bi.bmiHeader);
    bi.bmiHeader.biWidth = W;
    bi.bmiHeader.biHeight = -H; // top-down
    bi.bmiHeader.biPlanes = 1;
    bi.bmiHeader.biBitCount = 32;
    bi.bmiHeader.biCompression = BI_RGB;

    HDC screen = GetDC(NULL);
    HDC mem = CreateCompatibleDC(screen);
    DWORD *px = NULL;
    HBITMAP bmp = CreateDIBSection(screen, &bi, DIB_RGB_COLORS, (void **)&px, NULL, 0);
    SelectObject(mem, bmp);

    for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++) {
            px[y * W + x] = 0;                                            // transparent
            if (x < 6 || y < 6 || x >= W - 6 || y >= H - 6) put(px, x, y, 0, 255, 0, 255);     // cadre vert opaque
            else if (x > 100 && x < 300 && y > 100 && y < 300) put(px, x, y, 255, 0, 0, 128); // carré rouge 50 %
            else if (y > 330 && y < 380) put(px, x, y, 0, 128, 255, (BYTE)(x * 255 / W));    // dégradé d'alpha
        }

    POINT src = {0, 0}, dst = {200, 200};
    SIZE sz = {W, H};
    BLENDFUNCTION bf = {AC_SRC_OVER, 0, 255, AC_SRC_ALPHA};
    BOOL ok = UpdateLayeredWindow(hwnd, screen, &dst, &sz, mem, &src, 0, &bf, ULW_ALPHA);
    fprintf(logf, "UpdateLayeredWindow: %d (err %lu)\n", ok, GetLastError());
    fflush(logf);

    ShowWindow(hwnd, SW_SHOWNOACTIVATE);
    SetTimer(hwnd, 1, 60000, NULL);

    MSG msg;
    while (GetMessageW(&msg, NULL, 0, 0) > 0) {
        if (msg.message == WM_TIMER) break;
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    fprintf(logf, "fin\n");
    fclose(logf);
    return 0;
}
