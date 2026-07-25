import js from "@eslint/js";
import reactHooks from "eslint-plugin-react-hooks";
import globals from "globals";
import tseslint from "typescript-eslint";

export default tseslint.config(
  { ignores: ["dist", "node_modules", "../root/**"] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    files: ["**/*.{ts,tsx}"],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
    },
    plugins: {
      "react-hooks": reactHooks,
    },
    rules: {
      ...reactHooks.configs.recommended.rules,

      // All user-facing copy lives in the typed i18n catalog (DEC-021). This gate is what makes
      // that a rule rather than a habit — it fails the lint lane, locally and in CI.
      "no-restricted-syntax": [
        "error",
        {
          selector: "JSXText[value=/[A-Za-z]{2,}/]",
          message:
            "Hardcoded user-facing copy. Use the typed i18n catalog: t('some.key') from shared/i18n.",
        },
        {
          selector:
            "JSXAttribute[name.name=/^(title|placeholder|alt|aria-label)$/] > Literal[value=/[A-Za-z]{2,}/]",
          message:
            "Hardcoded user-facing copy in an attribute. Use the typed i18n catalog: t('some.key') from shared/i18n.",
        },
      ],
    },
  },
  {
    // The catalog is where copy is allowed to be a literal.
    files: ["shared/i18n/**"],
    rules: { "no-restricted-syntax": "off" },
  },
);
