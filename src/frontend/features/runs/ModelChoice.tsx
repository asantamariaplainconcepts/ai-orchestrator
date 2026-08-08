import { useAgentModels } from "@/features/automations/useAutomations";
import { t } from "@/shared/i18n";
import { Input } from "@/shared/ui/input";
import { NativeSelect } from "@/shared/ui/native-select";

/**
 * The model a launch chooses, for that Run only (#291) — the sibling of the runtime picker
 * beside it, and deliberately the same three states the Automation form renders: the runtime
 * listed its models, an operator configured them, or the machine could not be asked.
 *
 * The options follow the runtime selected **in this dialog**, so choosing a runtime and then a
 * model reads as one decision rather than two that can silently disagree.
 */
export function ModelChoice({
  runtime,
  value,
  onChange,
  enabled,
}: {
  runtime: string;
  value: string;
  onChange: (model: string) => void;
  enabled: boolean;
}) {
  const models = useAgentModels(runtime, enabled);
  const offered = models.data?.models ?? [];
  const source = models.data?.source;

  return (
    <div className="flex flex-col gap-2">
      <span className="text-sm font-medium">{t("automations.model")}</span>
      {offered.length > 0 ? (
        <NativeSelect
          value={value}
          onChange={(event) => onChange(event.target.value)}
          aria-label={t("automations.model")}
        >
          <option value="">{t("runs.runNow.resolvedModel")}</option>
          {(offered.includes(value) || !value ? offered : [value, ...offered]).map((candidate) => (
            <option key={candidate} value={candidate}>
              {candidate}
            </option>
          ))}
        </NativeSelect>
      ) : (
        <Input
          value={value}
          onChange={(event) => onChange(event.target.value)}
          aria-label={t("automations.model")}
          placeholder={t("runs.runNow.resolvedModel")}
        />
      )}
      <span className="text-xs text-muted-foreground">
        {!runtime
          ? t("automations.modelPickRuntimeFirst")
          : models.isPending
            ? t("automations.modelAsking")
            : source === "couldNotAsk"
              ? t("automations.modelCouldNotAsk")
              : source === "declared" && offered.length === 0
                ? t("automations.modelNoneDeclared")
                : source === "declared"
                  ? t("automations.modelDeclared")
                  : t("automations.modelEnumerated")}
      </span>
    </div>
  );
}
