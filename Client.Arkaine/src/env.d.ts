/// <reference types="vite/client" />

interface ImportMetaEnv {
    readonly VITE_ARKAINE_SERVER?: string
    readonly VITE_ARKAINE_VERSION?: string
}

interface ImportMeta {
    readonly env: ImportMetaEnv
}
