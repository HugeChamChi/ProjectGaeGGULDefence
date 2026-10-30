(() => {
  'use strict';
  const core = StudioCore;
  const studio = {...core};
  // Feature installation and execution order live here for both the browser and workbook engine.
  const features = [PlannerScopeFeature, UnitKeysFeature,...(typeof DroneOnlyFeature==='function'?[DroneOnlyFeature]:[]), AttendanceEventsFeature, MissionEventsFeature, ...(typeof BonusEditorFeature==='function'?[BonusEditorFeature]:[])]
    .map(install => install({core, studio}));
  const transform = (value, hook, reverse = false) =>
    (reverse ? [...features].reverse() : features).reduce((result, feature) => feature[hook] ? feature[hook](result) : result, value);
  const validationProject = project => transform(project, 'validationProject');

  Object.assign(studio, {
    create() { return transform(core.create(), 'decorate'); },
    shape(project) { return transform(core.shape(transform(project, 'prepare', true)), 'decorate'); },
    migrate(project) {
      return project.schemaVersion === 2 ? studio.shape(project) : transform(core.migrate(project), 'migrate');
    },
    newRecord(name, project) {
      return features.reduce((row, feature) => feature.newRecord ? feature.newRecord(row, name, project) : row, core.newRecord(name, project));
    },
    update(project, name, index, key, value) {
      core.update(project, name, index, key, value);
      for (const feature of features) feature.updated?.(project, name, index, key, value);
    },
    label(name, row) {
      return features.reduce((text, feature) => feature.label ? feature.label(text, name, row) : text, core.label(name, row));
    },
    validate(project) {
      let issues = core.validate(validationProject(project));
      for (const feature of features) {
        if (feature.filterIssues) issues = feature.filterIssues(issues);
        if (feature.validate) issues.push(...feature.validate(project));
      }
      return issues;
    },
    tables(project) {
      const errors = studio.validate(project).filter(issue => issue.severity === 'error');
      if (errors.length) throw Error(`검사 오류 ${errors.length}개를 먼저 수정하세요.`);
      return features.reduce((tables, feature) => feature.tables ? feature.tables(tables, project) : tables,
        core.tables(validationProject(project)));
    }
  });
  globalThis.Studio = Object.freeze(studio);
})();
