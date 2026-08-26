import eslint from '@eslint/js'
import globals from 'globals'
import pluginVue from 'eslint-plugin-vue'
import { withVueTs, vueTsConfigs } from '@vue/eslint-config-typescript'

export default withVueTs(
    eslint.configs.recommended,
    {
        name: 'arkaine/files-to-lint',
        files: ['**/*.{js,mjs,cjs,ts,mts,cts,vue}'],
        languageOptions: {
            globals: {
                ...globals.browser,
                ...globals.node
            }
        }
    },
    {
        name: 'arkaine/ignores',
        ignores: ['dist/**', 'node_modules/**']
    },
    pluginVue.configs['flat/essential'],
    vueTsConfigs.recommended,
    {
        rules: {
            'no-console': process.env.NODE_ENV === 'production' ? 'warn' : 'off',
            'no-debugger': process.env.NODE_ENV === 'production' ? 'warn' : 'off',
            'vue/script-indent': ['error', 4, { baseIndent: 1 }],
            'vue/html-quotes': ['error', 'double', { avoidEscape: false }],
            quotes: ['error', 'single'],
            'no-unreachable': 2,
            semi: [2, 'never'],
            'eol-last': 2,
            'vue/no-useless-template-attributes': 'off',
            'vue/multi-word-component-names': 'off',
            '@typescript-eslint/no-non-null-assertion': 'off'
        }
    }
)
