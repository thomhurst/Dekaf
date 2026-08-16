const experiencesScriptUrl =
  'https://cdn.jsdelivr.net/npm/@algolia/experiences/dist/experiences.js?appId=YX14GEKOQ8&apiKey=dfe6afaf9830f43f6c6271e15724446a&experienceId=YX14GEKOQ8&env=prod';

export default function algoliaExperiencesPlugin() {
  return {
    name: 'algolia-experiences',
    injectHtmlTags() {
      return {
        postBodyTags: [
          {
            tagName: 'script',
            attributes: {
              src: experiencesScriptUrl,
            },
          },
        ],
      };
    },
  };
}
