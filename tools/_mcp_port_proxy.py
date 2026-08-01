import asyncio


async def copy_stream(reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
    try:
        while data := await reader.read(65536):
            writer.write(data)
            await writer.drain()
    except (ConnectionError, asyncio.CancelledError):
        pass
    finally:
        try:
            writer.close()
            await writer.wait_closed()
        except ConnectionError:
            pass


async def proxy_client(reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
    upstream_reader, upstream_writer = await asyncio.open_connection("127.0.0.1", 8081)
    await asyncio.gather(
        copy_stream(reader, upstream_writer),
        copy_stream(upstream_reader, writer),
    )


async def main() -> None:
    server = await asyncio.start_server(proxy_client, "127.0.0.1", 8080)
    async with server:
        await server.serve_forever()


asyncio.run(main())
