import { http, HttpResponse } from "msw";

type LegacyRequest = {
  url: URL;
  params: Record<string, string | undefined>;
  json: () => Promise<unknown>;
};

type ResponseTransform = {
  status?: number;
  body?: unknown;
};

type LegacyContext = {
  status: (status: number) => ResponseTransform;
  json: (body: unknown) => ResponseTransform;
};

type LegacyResponse = (
  ...transforms: ResponseTransform[]
) => Response;

type LegacyHandler = (
  request: LegacyRequest,
  response: LegacyResponse,
  context: LegacyContext,
) => Response | Promise<Response>;

type HttpRequestResolver = (input: {
  request: Request;
  params: Record<string, string | undefined>;
}) => Promise<Response>;

type HttpMethod = (path: string, resolver: HttpRequestResolver) => unknown;

const context: LegacyContext = {
  status: (status) => ({ status }),
  json: (body) => ({ body }),
};

const response: LegacyResponse = (...transforms) => {
  const status = transforms.find((transform) => transform.status)?.status ?? 200;
  const bodyTransform = transforms.find((transform) => "body" in transform);

  return bodyTransform
    ? HttpResponse.json(bodyTransform.body, { status })
    : new HttpResponse(null, { status });
};

function createHandler(method: "get" | "post" | "patch") {
  const httpMethod = http[method] as unknown as HttpMethod;

  return (path: string, handler: LegacyHandler) =>
    httpMethod(path, async ({ request, params }) =>
      handler(
        {
          url: new URL(request.url),
          params,
          json: () => request.json(),
        },
        response,
        context,
      ));
}

// Keeps existing test handlers readable while the project uses MSW 2.x.
export const rest = {
  get: createHandler("get"),
  post: createHandler("post"),
  patch: createHandler("patch"),
};
