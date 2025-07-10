console.log("Swagger UI initialized with multiple specs");
window.onload = function () {
  window.ui = SwaggerUIBundle({
    urls: [
      { url: "/api/usuarios/swagger/v1/swagger.json", name: "Usuarios API" },
      {
        url: "/api/parametros/swagger/v1/swagger.json",
        name: "Parametros API",
      },
      { url: "/api/clientes/swagger/v1/swagger.json", name: "Clientes API" },
    ],
    dom_id: "#swagger-ui",
    deepLinking: true,
    presets: [SwaggerUIBundle.presets.apis, SwaggerUIStandalonePreset],
    layout: "StandaloneLayout",
  });
};
