﻿using Blog.Business.Application.ViewModels.Posts;
using Blog.Data;
using Blog.Data.Models;
using Blog.Identity.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Blog.Business.Services
{
    public class PostService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUser _user;

        public PostService(ApplicationDbContext context, IUser user)
        {
            _context = context;
            _user = user;
        }

        public async Task<IEnumerable<PostResumidoViewModel>> ObterPostsAsync(bool meusPosts = false)
        {
            var queryable = _context.Posts.Include(p => p.Autor).AsQueryable();

            if (meusPosts)
            {
                if (!(_user?.Autenticado() ?? false))
                    throw new UnauthorizedAccessException("Você deve estar logado para visualizar seus posts.");

                queryable = queryable.Where(p => p.AutorId == _user.UsuarioId().Value);
            }
            else
            {
                if (!(_user?.Autenticado() ?? false) || !_user.Administrador())
                    queryable = queryable.Where(p => !p.Excluido);
            }

            return await queryable.Select(p =>
                new PostResumidoViewModel
                {
                    Id = p.Id,
                    Titulo = p.Titulo,
                    Conteudo = p.Conteudo,
                    Excluido = p.Excluido,
                    DataHoraCriacao = p.DataHoraCriacao,
                    AutorId = p.AutorId,
                    AutorNomeCompleto = $"{p.Autor.Nome} {p.Autor.Sobrenome}"
                }
            ).ToListAsync();
        }

        public Task<Post> ObterPostPorIdAsync(Guid id) => _context.Posts.Include(p => p.Autor).FirstOrDefaultAsync(m => m.Id == id);

        public async Task<bool> PublicarPostAsync(Post post)
        {
            post.AutorId = _user.UsuarioId().Value;

            _context.Add(post);

            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<Post> EditarPostAsync(Post post)
        {
            var postOriginal = await _context.Posts.FindAsync(post.Id);
            if (postOriginal == null)
                return null;

            if (!_user.Administrador() && postOriginal.AutorId != _user.UsuarioId().Value)
                throw new UnauthorizedAccessException("Usuário não autorizado a editar o post pois não pertence ao mesmo.");

            postOriginal.Titulo = post.Titulo;
            postOriginal.Conteudo = post.Conteudo;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (await PostExists(post.Id))
                    throw;

                return null;
            }

            return postOriginal;
        }

        public async Task<bool> DeletarPostAsync(Guid id)
        {
            var post = await _context.Posts.FindAsync(id);
            if (post == null)
                return false;

            if (!_user.Administrador() && post.AutorId != _user.UsuarioId().Value)
                throw new UnauthorizedAccessException("Usuário não autorizado a excluir o post pois não pertence ao mesmo.");

            post.Excluido = true;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> AtivarPostAsync(Guid id)
        {
            var post = await _context.Posts.FindAsync(id);
            if (post == null)
                return false;

            if (!_user.Administrador() && post.AutorId != _user.UsuarioId().Value)
                throw new UnauthorizedAccessException("Usuário não autorizado a excluir o post pois não pertence ao mesmo.");

            post.Excluido = false;

            await _context.SaveChangesAsync();

            return true;
        }

        private async Task<bool> PostExists(Guid id) => await _context.Posts.AnyAsync(e => e.Id == id);
    }
}
