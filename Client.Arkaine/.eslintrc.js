module.exports = {
    root: true,
    env: {
      node: true
    },
    extends: [
      'plugin:vue/vue3-essential',
      'eslint:recommended',
      '@vue/typescript/recommended'
    ],
    parserOptions: {
      ecmaVersion: 2020
    },
    rules: {
      'no-console': process.env.NODE_ENV === 'production' ? 'warn' : 'off',
      'no-debugger': process.env.NODE_ENV === 'production' ? 'warn' : 'off',
      'vue/script-indent': ['error', 4, { baseIndent: 1 }],
      'quotes': ['error', 'single'],
      'no-unreachable': 2,
      'semi': [2, 'never'],
      'eol-last': 2,
      'vue/no-useless-template-attributes': 'off',
      'vue/multi-word-component-names': 'off'
    }
  }
